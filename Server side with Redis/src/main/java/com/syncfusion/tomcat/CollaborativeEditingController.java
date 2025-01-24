package com.syncfusion.tomcat;

import java.io.ByteArrayInputStream;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.messaging.MessageHeaders;
import org.springframework.stereotype.Component;
import org.springframework.web.bind.annotation.CrossOrigin;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.syncfusion.ej2.wordprocessor.ActionInfo;
import com.syncfusion.ej2.wordprocessor.CollaborativeEditingHandler;
import com.syncfusion.ej2.wordprocessor.DocumentOperation;
import com.syncfusion.ej2.wordprocessor.WordProcessorHelper;
import com.syncfusion.tomcat.controller.DocumentEditorHub;
import com.syncfusion.tomcat.controller.RedisSubscriber;
import org.springframework.core.io.Resource;

import redis.clients.jedis.Jedis;
import redis.clients.jedis.exceptions.JedisException;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.AwsCredentials;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.core.ResponseInputStream;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.model.GetObjectRequest;
import software.amazon.awssdk.services.s3.model.GetObjectResponse;
import software.amazon.awssdk.regions.Region;

@RestController
@Component
public class CollaborativeEditingController {

	@Value("classpath:static/files/*")
	private Resource[] resources;

	// Redis Configuration
	@Value("${spring.datasource.redishost}")
	private static String REDIS_HOST;
	@Value("${spring.datasource.redisport}")
	private static int REDIS_PORT;
	@Value("${spring.datasource.redispassword}")
	private String REDIS_PASSWORD;
	@Value("${spring.datasource.redisssl}")
	private boolean REDISSSL;

	@Value("${spring.datasource.accesskey}")
	private String datasourceAccessKey;
	@Value("${spring.datasource.secretkey}")
	private String datasourceSecretKey;
	@Value("${spring.datasource.bucketname}")
	private String datasourceBucketName;
	@Value("${spring.datasource.regionname}")
	private String datasourceRegionName;

	@Autowired
	private BackgroundService backgroundService;

	private final Gson gson;
	protected static String documentName;

	// Dependency injection through constructor
	@Autowired
	public CollaborativeEditingController(Gson gson) {
		this.gson = gson;
	}

	@CrossOrigin(origins = "*", allowedHeaders = "*")
	@PostMapping("/api/collaborativeediting/ImportFile")
	public String ImportFile(@RequestBody FilesPathInfo file) throws Exception {
		try {
			ClassLoader classLoader = getClass().getClassLoader();
			WordProcessorHelper document = getDocumentFromBucketS3(file.getFileName(), datasourceAccessKey,
			datasourceSecretKey, datasourceBucketName);
			documentName=file.getFileName();
			// Get the list of pending operations for the document
			List<ActionInfo> actions = getPendingOperations(file.getFileName(), 0, -1);
			if (actions != null && actions.size() > 0) {
				// If there are any pending actions, update the document with these actions
				document.updateActions(actions);
			}
			// Serialize the updated document to SFDT format
			String json = WordProcessorHelper.serialize(document);
			// Return the serialized content as a JSON string
			return json;
		} catch (Exception e) {
			e.printStackTrace();
			return "{\"sections\":[{\"blocks\":[{\"inlines\":[{\"text\":" + e.getMessage() + "}]}]}]}";
		}
	}

	// Method to retrieve pending operations from a Redis list between specified
	// indexes
	private List<ActionInfo> getPendingOperations(String listKey, int startIndex, int endIndex) {
		try (Jedis jedis = RedisSubscriber.getJedis()) {
			// Initialize the list to hold ActionInfo objects
			// List<ActionInfo> actionInfoList = new ArrayList<>();
			Object response = jedis.eval(CollaborativeEditingHelper.pendingOperations, 2, listKey,
					listKey + CollaborativeEditingHelper.actionsToRemoveSuffix, String.valueOf(startIndex),
					String.valueOf(endIndex));
			List<Object> results = (List<Object>) response;
			List<ActionInfo> actions = new ArrayList<>();
			for (Object result : results) {
				List<Object> resultList = (List<Object>) result;
				for (Object item : resultList) {
					actions.add(gson.fromJson((String) item, ActionInfo.class));
				}
			}
			return actions;
		} catch (Exception ex) {
			ex.printStackTrace();
			return null;
		}
	}

	@CrossOrigin(origins = "*", allowedHeaders = "*")
	@PostMapping("/api/collaborativeediting/UpdateAction")
	public ActionInfo UpdateAction(@RequestBody ActionInfo param) throws Exception {
		String roomName = param.getRoomName();
		ActionInfo transformedAction = addOperationsToCache(param);
		HashMap<String, Object> action = new HashMap<>();
		action.put("action", "updateAction");
		DocumentEditorHub.publishToRedis(roomName, transformedAction);
		DocumentEditorHub.broadcastToRoom(roomName, transformedAction, new MessageHeaders(action));
		return transformedAction;
	}

	@CrossOrigin(origins = "*", allowedHeaders = "*")
	@PostMapping("/api/collaborativeediting/GetActionsFromServer")
	public String GetActionsFromServer(@RequestBody ActionInfo param) throws ClassNotFoundException {
		try (Jedis jedis = RedisSubscriber.getJedis()) {
			// Initialize necessary variables from the parameters and helper class
			int saveThreshold = CollaborativeEditingHelper.saveThreshold;
			String roomName = param.getRoomName();
			int lastSyncedVersion = param.getVersion();
			int clientVersion = param.getVersion();
			// Fetch actions that are effective and pending based on the last synced version
			List<ActionInfo> actions = GetEffectivePendingVersion(roomName, lastSyncedVersion, jedis);
			List<ActionInfo> currentAction = new ArrayList<>();

			for (ActionInfo action : actions) {
				// Increment the version for each action sequentially
				action.setVersion(++clientVersion);

				// Filter actions to only include those that are newer than the client's last
				// known version
				if (action.getVersion() > lastSyncedVersion) {
					// Transform actions that have not been transformed yet
					if (!action.isTransformed()) {
						CollaborativeEditingHandler.transformOperation(action, new ArrayList<>(actions));
					}
					currentAction.add(action);
				}
			}
			// Serialize the filtered and transformed actions to JSON and return
			return gson.toJson(currentAction);
		} catch (Exception ex) {
			ex.printStackTrace();
			// In case of an exception, return an empty JSON object
			return "{}";
		}
	}

	private List<ActionInfo> GetEffectivePendingVersion(String roomName, int lastSyncedVersion, Jedis jedis) {
		// Define Redis keys for accessing the room data and its revision information
		String[] keys = { roomName, roomName + CollaborativeEditingHelper.revisionInfoSuffix };
		// Prepare Redis values for the script: start index and save threshold
		String[] values = { Integer.toString(lastSyncedVersion),
				String.valueOf(CollaborativeEditingHelper.saveThreshold) };
		Object response = jedis.eval(CollaborativeEditingHelper.effectivePendingOperations, keys.length, keys[0],
				keys[1], values[0], values[1]);
		// Deserialize the fetched actions from Redis and convert them into a list of
		// ActionInfo objects
		List<Object> results = (List<Object>) response;
		List<ActionInfo> actions = new ArrayList<>();
		for (Object result : results) {
			if (result instanceof String) {
				actions.add(gson.fromJson((String) result, ActionInfo.class));
			}
		}
		return actions;
	}

	private ActionInfo addOperationsToCache(ActionInfo action) throws Exception {
		int clientVersion = action.getVersion();
		// Serialize the action
		String serializedAction = gson.toJson(action);
		String roomName = action.getRoomName();

		// Define the keys for Redis operations based on the action's room name
		String[] keys = { roomName + CollaborativeEditingHelper.versionInfoSuffix, roomName,
				roomName + CollaborativeEditingHelper.revisionInfoSuffix,
				roomName + CollaborativeEditingHelper.actionsToRemoveSuffix };
		// Prepare values for the Redis script
		String[] values = { serializedAction, String.valueOf(clientVersion),
				String.valueOf(CollaborativeEditingHelper.saveThreshold) };

		try (Jedis jedis = RedisSubscriber.getJedis()) {
			try {
				// Execute the Lua script in Redis and store the results
				Object response = jedis.eval(CollaborativeEditingHelper.insertScript, keys.length, keys[0], keys[1],
						keys[2], keys[3], values[0], values[1], values[2]);
				List<Object> results = (List<Object>) response;
				// Parse the version number from the script results
				int version = Integer.parseInt(results.get(0).toString());
				// Deserialize the list of previous operations from the script results
				ArrayList<ActionInfo> previousOperations = new ArrayList();
				Object data = results.get(1);
				if (data instanceof List) {
					for (Object result : (List<?>) data) {
						if (result instanceof String) {
							previousOperations.add(gson.fromJson((String) result, ActionInfo.class));
						}
					}
				}
				// Increment the version for each previous operation
				previousOperations.forEach(op -> op.setVersion(op.getVersion() + 1));
				// Check if there are multiple previous operations to determine if
				// transformation is needed
				if (previousOperations.size() > 1) {
					// Set the current action to the last operation in the list
					action = previousOperations.get(previousOperations.size() - 1);
					for (ActionInfo op : previousOperations) {
						// Transform operations that have not been transformed yet
						List<DocumentOperation> operation = op.getOperations();
						if (operation != null && !op.isTransformed()) {
							CollaborativeEditingHandler.transformOperation(op, previousOperations);
						}
					}
				}
				// Update the action's version and mark it as transformed
				action.setVersion(version);
				action.setTransformed(true);
				// Update the record in the cache with the new version
				updateRecordToCache(version, action, jedis);
				// Check if there are cleared operations to be saved
				if (results.size() > 2 && results.get(2) != null) {
					autoSaveChangesToSourceDocument((List<Object>) results.get(2), action);
				}

			} catch (Exception e) {
				e.printStackTrace();
			}
		} catch (JedisException e) {
			e.printStackTrace();
		}
		// Return the updated action
		return action;
	}

	private void autoSaveChangesToSourceDocument(List<Object> clearedOperations, ActionInfo action) {
		List<ActionInfo> actions = new ArrayList<>();
		for (Object operation : clearedOperations) {
			ActionInfo actionInfo = gson.fromJson((String) operation, ActionInfo.class);
			actions.add(actionInfo);
		}
		// Prepare the message for saving the cleared operations
		SaveInfo message = new SaveInfo();
		message.setActions(actions);
		message.setPartialSave(true);
		message.setRoomName(action.getRoomName());
		backgroundService.addItemToProcess(message);
	}

	private void updateRecordToCache(int version, ActionInfo action, Jedis jedis) {
		// Serialize the action
		String serializedAction = gson.toJson(action);

		// Prepare Redis keys and values for the script execution
		String roomName = action.getRoomName();
		String revisionInfoKey = roomName + CollaborativeEditingHelper.revisionInfoSuffix;
		String previousVersion = String.valueOf(version - 1);
		String saveThreshold = String.valueOf(CollaborativeEditingHelper.saveThreshold);

		// Execute the Lua script with the prepared keys and values
		try {
			jedis.eval(CollaborativeEditingHelper.updateRecord, 2, roomName, revisionInfoKey, serializedAction,
					previousVersion, saveThreshold);
		} catch (Exception ex) {
			ex.printStackTrace();
			// Handle the exception as needed, e.g., logging or rethrowing
		}
	}
	protected static WordProcessorHelper getDocumentFromBucketS3(String documentId, String accessKey, String secretKey,
			String bucketName) {	
		try {
			AwsCredentials credentials = AwsBasicCredentials.create(accessKey, secretKey);
			StaticCredentialsProvider credentialsProvider = StaticCredentialsProvider.create(credentials);
			S3Client s3Client = S3Client.builder().region(Region.US_EAST_1).credentialsProvider(credentialsProvider)
					.build();
			ResponseInputStream<GetObjectResponse> objectData = s3Client
					.getObject(GetObjectRequest.builder().bucket(bucketName).key(documentId).build());
			// Read the object data into a byte array
			ByteArrayOutputStream byteArrayOutputStream = new ByteArrayOutputStream();
			byte[] buffer = new byte[1024];
			int bytesRead;
			while ((bytesRead = objectData.read(buffer)) != -1) {
				byteArrayOutputStream.write(buffer, 0, bytesRead);
			}
			s3Client.close();
			byte[] data = byteArrayOutputStream.toByteArray();
			// Create an input stream from the byte array
			try (InputStream stream = new ByteArrayInputStream(data)) {
				return WordProcessorHelper.load(stream, true);
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
		return null;
	}

}
