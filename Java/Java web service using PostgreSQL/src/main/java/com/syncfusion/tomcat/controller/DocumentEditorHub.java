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
	public static HashMap<String, UserActionInfo> userActions = new HashMap<String, UserActionInfo>();
	public static HashMap<String, ActionInfo> actions = new HashMap<String, ActionInfo>();
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
			@DestinationVariable String documentName) {

		String connectionId = headerAccessor.getSessionId();

		info.setConnectionId(connectionId);
		String docName = info.getRoomName();
		// info.setAction("connectionId");
		HashMap<String, Object> additionalHeaders = new HashMap<>();
		additionalHeaders.put("action", "connectionId");
		MessageHeaders headers = new MessageHeaders(additionalHeaders);
		broadcastToRoom(docName, info, headers);

		if (!actions.containsKey(connectionId)) {
			actions.put(connectionId, info);
		}
		ArrayList<ActionInfo> actionsList = roomList.computeIfAbsent(documentName, k -> new ArrayList<>());
		// Add the new user info to the list
		actionsList.add(info);
		HashMap<String, Object> addUser = new HashMap<>();
		addUser.put("action", "addUser");
		MessageHeaders addUserheaders = new MessageHeaders(addUser);
		broadcastToRoom(docName, actionsList, addUserheaders);
	}

	@EventListener
	public void handleWebSocketDisconnectListener(SessionDisconnectEvent event) throws Exception {
		String sessionId = event.getSessionId();
		HashMap<String, ActionInfo> userDetails = DocumentEditorHub.actions;
		if (userDetails.containsKey(sessionId)) {
			ActionInfo info = userDetails.get(sessionId);
			
			String docName = info.getRoomName();
			HashMap<String, Object> removeUser = new HashMap<>();
			removeUser.put("action", "removeUser");
			MessageHeaders removeUserheaders = new MessageHeaders(removeUser);
			broadcastToRoom(docName, info, removeUserheaders);
			userDetails.remove(sessionId);
			ArrayList<ActionInfo> actionsList = roomList.computeIfAbsent(info.getRoomName(), k -> new ArrayList<>());
			for (ActionInfo action : actionsList) {

				if (action.getConnectionId() == sessionId) {
					actionsList.remove(action);
					break;
				}
			}
			if (userDetails.isEmpty()) {
				Connection connection = DriverManager.getConnection(datasourceUrl, datasourceUsername,
						datasourcePassword);
				CollaborativeEditingController.updateOperationsToSourceDocument(docName, false, 0, connection,
						datasourceAccessKey, datasourceSecretKey, datasourceBucketName);
			}
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
