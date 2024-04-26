package com.syncfusion.tomcat.controller;

import java.sql.Connection;
import java.sql.DriverManager;
import java.util.ArrayList;
import java.util.HashMap;

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

import com.syncfusion.ej2.wordprocessor.UserActionInfo;
import com.syncfusion.tomcat.CollaborativeEditingController;

import redis.clients.jedis.Jedis;
import redis.clients.jedis.exceptions.JedisConnectionException;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.syncfusion.ej2.wordprocessor.ActionInfo;

@Controller
public class DocumentEditorHub {	
	public static final HashMap<String, ArrayList<ActionInfo>> roomList = new HashMap<>();
	public static SimpMessagingTemplate messagingTemplate;
	private static final int MAX_RETRIES = 5;
	private static final long RETRY_INTERVAL_MS = 1000;
	public DocumentEditorHub(SimpMessagingTemplate messagingTemplate) {
		this.messagingTemplate = messagingTemplate;
	}

	@Value("${spring.datasource.url}")
	public String datasourceUrl;
	@Value("${spring.datasource.username}")
	private String datasourceUsername;
	@Value("${spring.datasource.password}")
	private String datasourcePassword;

	// Bucket S3
	@Value("${spring.datasource.accesskey}")
	private String datasourceAccessKey;
	@Value("${spring.datasource.secretkey}")
	private String datasourceSecretKey;
	@Value("${spring.datasource.bucketname}")
	private String datasourceBucketName;
	@Value("${spring.datasource.regionname}")
	private String datasourceRegionName;

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
		// send the conection Id to the client
		broadcastToRoom(docName, info, headers);
		try (Jedis jedis = jedisPool.getResource()) {
			// to maintain the session id with its corresponding ActionInfo details.
			jedis.hset("documentMap", connectionId, documentName);
			// add the user details to the Redis cache
			jedis.sadd(docName, new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(info));	
			// Subscribe to the room, so that all users can get the JOIN/LEAVE notification
			joinLeaveUsersubscribe(docName);
			// publish the user list to the redis
			jedis.publish(docName, "JOIN|" + connectionId);			 
			
		} catch (JedisConnectionException e) {
			System.out.println(e);
		}
	}

	@EventListener
	public void handleWebSocketDisconnectListener(SessionDisconnectEvent event) throws Exception {
		String sessionId = event.getSessionId();
		try (Jedis jedis = jedisPool.getResource()) {
            //to get the user details of the provided sessionId
			String docName = jedis.hget("documentMap", sessionId);
			// Publish a message indicating the user's departure from the group
			jedis.publish(docName, "LEAVE|" + sessionId);			
		} catch (JedisConnectionException e) {
			System.out.println(e);
		}
	}

	private void joinLeaveUsersubscribe(String docName) {
		new Thread(() -> {
			try (Jedis jedis = jedisPool.getResource()) {
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
				}, docName);
			} catch (JedisConnectionException e) {
				System.out.println(e);
			}
		}).start();
	}

	public void notifyUsers(String docName, String eventType, String sessionId) {
		try (Jedis jedis = jedisPool.getResource()) {
			if ("JOIN".equals(eventType)) {
				HashMap<String, Object> addUser = new HashMap<>();
				addUser.put("action", "addUser");
				MessageHeaders addUserheaders = new MessageHeaders(addUser);
				// get the list of users from Redis
				Set<String> userJsonStrings = jedis.smembers(docName);
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
				//Boradcast the user list to all the users connected in that room
				broadcastToRoom(docName, actionsList, addUserheaders);
			} else if ("LEAVE".equals(eventType)) {
				// get the user list from the redis
				Set<String> userJsonStrings = jedis.smembers(docName);
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
					Connection connection = null;
					try {
						connection = DriverManager.getConnection(datasourceUrl, datasourceUsername, datasourcePassword);
					} catch (SQLException e1) {
						e1.printStackTrace();
					}
					try {
						TomcatApplication.updateOperationsToSourceDocument(docName,
								"doc_18d1e2a949604a1f8430710ae19aa354", false, 0, connection);
					} catch (Exception e) {
						e.printStackTrace();
					}
					jedis.del(docName);
				}
			}

		} catch (JedisConnectionException e) {
			System.out.println(e);
		}

	}

	public static void broadcastToRoom(String roomName, Object payload, MessageHeaders headers) {
		
		{			
			messagingTemplate.convertAndSend("/topic/public/" + roomName,
					MessageBuilder.createMessage(payload, headers));
		}
	}	
	public static void publishToRedis(String roomName, Object payload) throws JsonProcessingException {
		int retries = 0;
		while (retries < MAX_RETRIES) {
			try (Jedis jedis = RedisSubscriber.jedisPool.getResource()) {
				// try(Jedis jedis = RedisSubscriber.jedisConnection){	
				
				jedis.publish("collaborativedtiting",
						new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(payload));
				System.out.println("Message published to Redis"
						+ new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(payload));				
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
