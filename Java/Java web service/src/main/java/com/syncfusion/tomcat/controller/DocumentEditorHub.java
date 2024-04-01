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
import com.syncfusion.ej2.wordprocessor.ActionInfo;

@Controller
public class DocumentEditorHub {
	public static HashMap<String, UserActionInfo> userActions = new HashMap<String, UserActionInfo>();
	public static HashMap<String, ActionInfo> actions = new HashMap<String, ActionInfo>();
	public static final HashMap<String, ArrayList<ActionInfo>> roomList = new HashMap<>();
	public static SimpMessagingTemplate messagingTemplate;

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
		if (payload instanceof HashMap) {
			HashMap<String, ActionInfo> actionsMap = (HashMap<String, ActionInfo>) payload;
			ArrayList<ActionInfo> actionsList = new ArrayList<>(actionsMap.values());
			// messagingTemplate.convertAndSend("/topic/public/" + roomName, actionsList);
			messagingTemplate.convertAndSend("/topic/public/" + roomName,
					MessageBuilder.createMessage(actionsList, headers));
		} else {
			// messagingTemplate.convertAndSend("/topic/public/" + roomName, payload);
			messagingTemplate.convertAndSend("/topic/public/" + roomName,
					MessageBuilder.createMessage(payload, headers));
		}

	}
}
