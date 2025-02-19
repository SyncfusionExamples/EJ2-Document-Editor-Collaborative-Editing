package com.syncfusion.tomcat.controller;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.event.EventListener;
import org.springframework.messaging.MessageHeaders;
import org.springframework.messaging.handler.annotation.DestinationVariable;
import org.springframework.messaging.handler.annotation.MessageMapping;
import org.springframework.messaging.simp.SimpMessageHeaderAccessor;
import org.springframework.messaging.simp.SimpMessagingTemplate;
import org.springframework.messaging.support.MessageBuilder;
import org.springframework.stereotype.Controller;
import org.springframework.web.socket.messaging.SessionDisconnectEvent;

import com.syncfusion.tomcat.CollaborativeEditingHelper;
import redis.clients.jedis.Jedis;
import redis.clients.jedis.JedisPool;
import redis.clients.jedis.JedisPoolConfig;
import redis.clients.jedis.JedisPubSub;
import redis.clients.jedis.exceptions.JedisConnectionException;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonMappingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.syncfusion.ej2.wordprocessor.ActionInfo;

@Controller
public class DocumentEditorHub {

	// Redis Configuration
	@Value("${spring.datasource.redishost}")
	private String REDIS_HOST;
	@Value("${spring.datasource.redisport}")
	private int REDIS_PORT;
	@Value("${spring.datasource.redispassword")
	private String REDIS_PASSWORD;

	public static SimpMessagingTemplate messagingTemplate;
	private static final int MAX_RETRIES = 5;
	private static final long RETRY_INTERVAL_MS = 1000;
	static ObjectMapper mapper = new ObjectMapper();

	@Autowired
	public DocumentEditorHub(SimpMessagingTemplate messagingTemplate) {
		DocumentEditorHub.messagingTemplate = messagingTemplate;
	}

	@MessageMapping("/join/{documentName}")
	public void joinGroup(ActionInfo info, SimpMessageHeaderAccessor headerAccessor,
			@DestinationVariable String documentName) throws JsonProcessingException {
		// To get the connection Id
		String connectionId = headerAccessor.getSessionId();
		info.setConnectionId(connectionId);
		String docName = info.getRoomName();
		HashMap<String, Object> additionalHeaders = new HashMap<>();
		additionalHeaders.put("action", "connectionId");
		MessageHeaders headers = new MessageHeaders(additionalHeaders);
		// send the connection Id to the client
		broadcastToRoom(docName, info, headers);
		JedisPoolConfig poolConfig = new JedisPoolConfig();
		poolConfig.setMaxTotal(50);
		JedisPool jedisPool = new JedisPool(poolConfig, REDIS_HOST, REDIS_PORT);
		try (Jedis jedis = RedisSubscriber.getJedis()) {
			// to maintain the session id with its corresponding ActionInfo details.
			jedis.hset("documentMap", connectionId, documentName);
			// add the user details to the Redis cache
			String openedDocName = docName + CollaborativeEditingHelper.userInfoSuffix;
			jedis.rpush(openedDocName, mapper.writeValueAsString(info));
			// Subscribe to the room, so that all users can get the JOIN/LEAVE notification
			joinLeaveUsersubscribe(openedDocName);
			// publish the user list to the redis
			jedis.publish(openedDocName, "JOIN|" + connectionId);

		} catch (JedisConnectionException e) {
			System.out.println(e);
		}
	}

	@EventListener
	public void handleWebSocketDisconnectListener(SessionDisconnectEvent event) throws Exception {
		String sessionId = event.getSessionId();
		try (Jedis jedis = RedisSubscriber.getJedis()) {
			// to get the user details of the provided sessionId
			String docName = jedis.hget("documentMap", sessionId);
			// Publish a message indicating the user's departure from the group
			jedis.publish(docName, "LEAVE|" + sessionId);
		} catch (JedisConnectionException e) {
			System.out.println(e);
		}
	}

	private void joinLeaveUsersubscribe(String openedDocName) {
		new Thread(() -> {
			try (Jedis jedis = RedisSubscriber.getJedis()) {
				jedis.subscribe(new JedisPubSub() {
					@Override
					public void onMessage(String channel, String message) {
						String[] parts = message.split("\\|");
						if (parts.length == 2) {
							String eventType = parts[0];
							String sessionId = parts[1];
							notifyUsers(channel, eventType, sessionId);
						}
					}
				}, openedDocName);
			} catch (JedisConnectionException e) {
				System.out.println(e);
			}
		}).start();
	}

	public void notifyUsers(String docName, String eventType, String sessionId) {
		try (Jedis jedis = RedisSubscriber.getJedis()) {
			if ("JOIN".equals(eventType)) {
				HashMap<String, Object> addUser = new HashMap<>();
				addUser.put("action", "addUser");
				MessageHeaders addUserheaders = new MessageHeaders(addUser);
				// get the list of users from Redis
				String type = jedis.type(docName);
				List<String> userJsonStrings = jedis.lrange(docName, 0, -1);
				System.out.println("userJsonStrings to join" + userJsonStrings);
				ArrayList<ActionInfo> actionsList = new ArrayList<>();
				ObjectMapper mapper = new ObjectMapper();
				for (String userJson : userJsonStrings) {
					try {
						ActionInfo actionInfo = mapper.readValue(userJson, ActionInfo.class);
						actionsList.add(actionInfo);
					} catch (Exception e) {
						System.err.println("Error parsing user information JSON: " + e.getMessage());
					}
				}
				// Broadcast the user list to all the users connected in that room
				broadcastToRoom(docName, actionsList, addUserheaders);
			} else if ("LEAVE".equals(eventType)) {
				// get the user list from the redis
				List<String> userJsonStrings = jedis.lrange(docName, 0, -1);
				System.out.println("userJsonStrings to leave" + userJsonStrings);
				if (!userJsonStrings.isEmpty()) {
					ObjectMapper mapper = new ObjectMapper();
					for (String userJson : userJsonStrings) {
						ActionInfo action = null;
						try {
							action = mapper.readValue(userJson, ActionInfo.class);
						} catch (JsonMappingException e) {
							e.printStackTrace();
						} catch (JsonProcessingException e) {
							e.printStackTrace();
						}
						if (action.getConnectionId().equals(sessionId)) {
							// Remove the user from the user list
							jedis.srem(docName, userJson);
							HashMap<String, Object> removeUser = new HashMap<>();
							removeUser.put("action", "removeUser");
							MessageHeaders removeUserheaders = new MessageHeaders(removeUser);
							// Broadcast the removal notification to all users in the document
							broadcastToRoom(docName, action, removeUserheaders);
							// Remove the session ID from the session-document mapping
							jedis.hdel("documentMap", sessionId);
							break;
						}
					}
				} else {
					System.out.println("No users found in the document.");
				}
				if (userJsonStrings.isEmpty()) {
					jedis.del(docName);
				}
			}

		} catch (JedisConnectionException e) {
			System.out.println(e);
		}

	}

	public static void broadcastToRoom(String roomName, Object payload, MessageHeaders headers) {
		messagingTemplate.convertAndSend("/topic/public/" + roomName, MessageBuilder.createMessage(payload, headers));
	}

	public static void publishToRedis(String roomName, Object payload) throws JsonProcessingException {
		int retries = 0;
		while (retries < MAX_RETRIES) {
			try (Jedis jedis = RedisSubscriber.getJedis()) {

				jedis.publish("collaborativedtiting", mapper.writeValueAsString(payload));
				System.out.println("Message published to Redis" + mapper.writeValueAsString(payload));
				break;
			} catch (JedisConnectionException e) {
				retries++;
				System.out.println("Connection failed. Retrying in " + RETRY_INTERVAL_MS + " milliseconds...");
				try {
					Thread.sleep(RETRY_INTERVAL_MS);
				} catch (InterruptedException ex) {
					Thread.currentThread().interrupt();
				}
			}
		}
	}
}