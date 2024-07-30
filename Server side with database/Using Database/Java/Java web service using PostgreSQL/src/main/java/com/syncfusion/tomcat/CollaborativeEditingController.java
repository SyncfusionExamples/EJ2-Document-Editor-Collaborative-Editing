package com.syncfusion.tomcat;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;

import javax.sql.DataSource;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.messaging.MessageHeaders;
import org.springframework.web.bind.annotation.CrossOrigin;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonMappingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.google.gson.Gson;
import com.syncfusion.docio.WordDocument;
import com.syncfusion.ej2.wordprocessor.ActionInfo;
import com.syncfusion.ej2.wordprocessor.CollaborativeEditingHandler;
import com.syncfusion.ej2.wordprocessor.WordProcessorHelper;
import com.syncfusion.javahelper.system.OutSupport;
import com.syncfusion.tomcat.controller.DocumentEditorHub;

import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.AwsCredentials;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.core.ResponseInputStream;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.model.GetObjectRequest;
import software.amazon.awssdk.services.s3.model.GetObjectResponse;
import software.amazon.awssdk.services.s3.model.PutObjectRequest;

@RestController
public class CollaborativeEditingController {
	private static short saveThreshold = 200;
	// SQL
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

	@Autowired
	private DataSource dataSource;

	@CrossOrigin(origins = "*", allowedHeaders = "*")
	@PostMapping("/api/collaborativeediting/ImportFile")
	public String ImportFile(@RequestBody FilesPathInfo file) throws Exception {
		try {
			DocumentContent content = new DocumentContent();

			// Load the document from S3 bucket.
			WordProcessorHelper document = getDocumentFromBucketS3(file.getFileName(), datasourceAccessKey,
					datasourceSecretKey, datasourceBucketName);

			int lastSyncedVersion = 0;
			OutSupport<Integer> lastSyncedVersion_out = new OutSupport<Integer>(Integer.class);
			// Create table in database to store temporary data for the collaborative
			// editing session.
			// Room name is a unique identifier of the document for collaborative editing.
			// We need to maintain the unique ID to map it with the original document to
			// save it in the S3.			
				
			ArrayList<ActionInfo> actions = createRecordForCollaborativeEditing(file.getRoomName(),
					lastSyncedVersion_out);

			if (actions != null && actions.size() > 0) {
				// When new user join the collaborative editing session
				// Get previous editing operation from DB and update it in document editor.
				document.updateActions(actions);
				ActionInfo lastAction = actions.get(actions.size() - 1);
				lastSyncedVersion = lastAction.getVersion();
			}
			String json = WordProcessorHelper.serialize(document);
			content.setVersion(lastSyncedVersion);
			content.setSfdt(json);
			Gson gson = new Gson();
			String data = gson.toJson(content);
			return data;
		} catch (Exception e) {
			e.printStackTrace();
			return "{\"sections\":[{\"blocks\":[{\"inlines\":[{\"text\":" + e.getMessage() + "}]}]}]}";
		}
	}

	@CrossOrigin(origins = "*", allowedHeaders = "*")
	@PostMapping("/api/collaborativeediting/UpdateAction")
	public ActionInfo UpdateAction(@RequestBody ActionInfo param) throws Exception {
		String roomName = param.getRoomName();
		ActionInfo transformedAction = addOperationsToTable(param);
		HashMap<String, Object> action = new HashMap<>();
		action.put("action", "updateAction");
		DocumentEditorHub.publishToRedis(roomName, transformedAction);
		//DocumentEditorHub.broadcastToRoom(roomName, transformedAction, new MessageHeaders(action));
		return transformedAction;
	}

	@CrossOrigin(origins = "*", allowedHeaders = "*")
	@PostMapping("/api/collaborativeediting/GetActionsFromServer")
	public String GetActionsFromServer(@RequestBody ActionInfo param) throws ClassNotFoundException {
		String tableName = param.getRoomName();
		String getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version > " + param.getVersion();
		try (Connection connection = dataSource.getConnection()) {
			try (PreparedStatement updateCommand = connection.prepareStatement(getOperation)) {
				ResultSet reader = updateCommand.executeQuery();
				ArrayList<ActionInfo> actions;
				ArrayList<Map<String, Object>> table = getOperationsFromDatabaseResult(reader);
				if (!table.isEmpty()) {
					int startVersion = Integer.parseInt(table.get(0).get("version").toString());
					int lowestVersion = getLowestClientVersion(table);
					if (startVersion > lowestVersion) {
						String updatedOperation = "SELECT * FROM \"" + tableName + "\" WHERE version >= "
								+ lowestVersion;
						try (PreparedStatement command = connection.prepareStatement(updatedOperation)) {
							ResultSet reader2 = command.executeQuery();
							table.clear();
							table = getOperationsFromDatabaseResult(reader2);
						}
					}
					actions = getOperationsQueue(table);
					for (ActionInfo info : actions) {
						if (!info.isTransformed()) {
							CollaborativeEditingHandler.transformOperation(info, actions);
						}
					}
					// Assuming getIsTransformed() and Version are properties in ActionInfo
					actions.removeIf(x -> x.getVersion() <= param.getVersion());
					return new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(actions);
				}
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
		return "{}";
	}

	private ActionInfo addOperationsToTable(ActionInfo action) throws Exception {
		int clientVersion = action.getVersion();
		String roomName = action.getRoomName();
		String value = new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(action);
		String query = "INSERT INTO \"" + roomName + "\" (operation, clientVersion) VALUES (?, ?) RETURNING version;";

		try (Connection connection = dataSource.getConnection()) {
			PreparedStatement preparedStatement = connection.prepareStatement(query);
			preparedStatement.setString(1, value);
			preparedStatement.setInt(2, action.getVersion());
			try {
				ResultSet resultSet = preparedStatement.executeQuery();
				if (resultSet.next()) {
					int updateVersion = resultSet.getInt("version");
					if (updateVersion - clientVersion == 1) {
						action.setVersion(updateVersion);
						updateCurrentActionToDB(roomName, action, connection);
					} else {
						ArrayList<Map<String, Object>> table = getOperationsToTransform(roomName, clientVersion + 1,
								updateVersion, connection);
						int startVersion = (int) table.get(0).get("version");
						int lowestVersion = getLowestClientVersion(table);
						if (startVersion > lowestVersion) {
							table = getOperationsToTransform(roomName, lowestVersion, updateVersion, connection);
						}
						ArrayList<ActionInfo> actions = getOperationsQueue(table);
						for (ActionInfo info : actions) {
							if (!info.isTransformed()) {
								CollaborativeEditingHandler.transformOperation(info, actions);
							}
						}
						action = actions.get(actions.size() - 1);
						action.setVersion(updateVersion);
						updateCurrentActionToDB(roomName, actions.get(actions.size() - 1), connection);
					}
					if (updateVersion % saveThreshold == 0) {
						// Update the operations to table once specified threshold is reached.
						updateOperationsToSourceDocument(roomName, true, updateVersion, connection, datasourceAccessKey,
								datasourceSecretKey, datasourceBucketName);
					}
				}
			} catch (Exception e) {
				e.printStackTrace();
			}
		}
		return action;
	}

	private int getLowestClientVersion(ArrayList<Map<String, Object>> table) {
		int clientVersion = (int) table.get(0).get("clientVersion");
		for (Map<String, Object> row : table) {
			int version = Integer.parseInt(row.get("clientVersion").toString());
			if (version < clientVersion) {
				clientVersion = version;
			}
		}
		return clientVersion;
	}

	private ArrayList<Map<String, Object>> getOperationsToTransform(String tableName, int clientVersion,
			int currentVersion, Connection connection) throws ClassNotFoundException {
		String getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version BETWEEN ? AND ?";
		try (PreparedStatement command = connection.prepareStatement(getOperation)) {
			command.setInt(1, clientVersion);
			command.setInt(2, currentVersion);
			try (ResultSet resultSet = command.executeQuery()) {
				ArrayList<Map<String, Object>> resultList = getOperationsFromDatabaseResult(resultSet);
				return resultList;
			}
		} catch (SQLException e) {
			e.printStackTrace();
			return null;
		}
	}

	private void updateCurrentActionToDB(String tableName, ActionInfo action, Connection connection) {
		action.setTransformed(true);
		String updateQuery = "UPDATE \"" + tableName + "\" SET operation = ? WHERE version = " + action.getVersion();
		try (PreparedStatement updateStatement = connection.prepareStatement(updateQuery)) {
			updateStatement.setString(1, new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(action));
			updateStatement.executeUpdate();
		} catch (SQLException | com.fasterxml.jackson.core.JsonProcessingException e) {
			e.printStackTrace();
		}
	}

	public static void updateOperationsToSourceDocument(String roomName, boolean partialSave, int endVersion,
			Connection connection, String accessKey, String secretKey, String bucketName) throws Exception {

		int lastSyncedVersion = getLastSavedVersion(connection, roomName);
		String getOperation = "";
		if (partialSave) {
			getOperation = "SELECT * FROM \"" + roomName + "\" WHERE version BETWEEN " + (lastSyncedVersion + 1)
					+ " AND " + endVersion;
		} else {
			getOperation = "SELECT * FROM \"" + roomName + "\" WHERE version > " + lastSyncedVersion;
		}

		try (PreparedStatement command = connection.prepareStatement(getOperation)) {
			ResultSet reader = command.executeQuery();
			ArrayList<Map<String, Object>> table = getOperationsFromDatabaseResult(reader);
			ArrayList<ActionInfo> actions = getOperationsQueue(table);
			for (ActionInfo info : actions) {
				if (!info.isTransformed()) {
					CollaborativeEditingHandler.transformOperation(info, actions);
				}
			}
			reader.close();
			command.close();

			// room name is unique identifier used for editing the document in collaborative
			// editing.
			// Please get the actual file name by mapping the room name.
			String fileName = "";

			CollaborativeEditingHandler handler = new CollaborativeEditingHandler(
					getDocumentFromBucketS3(fileName, accessKey, secretKey, bucketName));
			for (ActionInfo info : actions) {
				handler.updateAction(info);
			}
			ByteArrayOutputStream outputStream = new ByteArrayOutputStream();
			WordDocument doc = WordProcessorHelper.save(WordProcessorHelper.serialize(handler.getDocument()));
			doc.save(outputStream, com.syncfusion.docio.FormatType.Docx);
			byte[] data = outputStream.toByteArray();
			outputStream.close();
			doc.close();

			// Save the document to S3 bucket
			AwsCredentials credentials = AwsBasicCredentials.create(accessKey, secretKey);
			StaticCredentialsProvider credentialsProvider = StaticCredentialsProvider.create(credentials);
			S3Client s3Client = S3Client.builder().region(Region.US_EAST_1).credentialsProvider(credentialsProvider)
					.build();
			PutObjectRequest objectRequest = PutObjectRequest.builder().bucket(bucketName).key(roomName).build();
			s3Client.putObject(objectRequest, software.amazon.awssdk.core.sync.RequestBody.fromBytes(data));
			s3Client.close();

			if (!partialSave) {
				deleteLastModifiedVersion(roomName, connection);
				dropTable(roomName, connection);
			} else {
				updateModifiedVersion(roomName, connection, endVersion);
			}
		}

		// connection.commit();
		/*
		 * } catch (Exception e) { e.printStackTrace(); }
		 */
	}

	private static void updateModifiedVersion(String roomName, Connection connection, int lastSavedVersion)
			throws SQLException {
		String tableName = "de_version_info";
		String query = "UPDATE \"" + tableName + "\" SET lastSavedVersion = ? where roomName= '" + roomName + "'";
		PreparedStatement preparedStatement = connection.prepareStatement(query);
		preparedStatement.setInt(1, lastSavedVersion);
		preparedStatement.executeUpdate();
	}

	private static void deleteLastModifiedVersion(String roomName, Connection connection) throws SQLException {
		String tableName = "de_version_info";
		String query = "DELETE FROM \"" + tableName + "\" WHERE roomName= '" + roomName + "'";
		PreparedStatement preparedStatement = connection.prepareStatement(query);
		preparedStatement.executeUpdate();
	}

	public static void dropTable(String documentId, Connection connection) throws ClassNotFoundException {
		try {
			// Drop the table
			String sqlQuery = "DROP TABLE \"" + documentId + "\"";
			try (PreparedStatement sqlCommand = connection.prepareStatement(sqlQuery)) {
				sqlCommand.executeUpdate();
			}
		} catch (SQLException e) {
			e.printStackTrace();
		}
	}

	private ArrayList<ActionInfo> createRecordForCollaborativeEditing(String roomName,
			OutSupport<Integer> lastSyncedVersion) throws SQLException, ClassNotFoundException, IOException {
		try (Connection connection = dataSource.getConnection()) {
			if (!tableExists(connection, roomName)) {
				lastSyncedVersion.setValue(0);
				// If record not present, create new table
				String queryString = "CREATE TABLE \"" + roomName
						+ "\" (version SERIAL PRIMARY KEY, operation TEXT, clientVersion INTEGER)";
				PreparedStatement preparedStatement = connection.prepareStatement(queryString);
				preparedStatement.executeUpdate();
				// Create table to track the last saved version.
				createRecordForVersionInfo(connection, roomName);
			} else {
				lastSyncedVersion.setValue(getLastSavedVersion(connection, roomName));
				// If record present, get previous editing operation from the db record.
				String queryString = "SELECT * FROM \"" + roomName + "\" WHERE version > "
						+ lastSyncedVersion.getValue();
				try (PreparedStatement updateCommand = connection.prepareStatement(queryString)) {
					ResultSet result = updateCommand.executeQuery();
					ArrayList<ActionInfo> actions = getOperationsQueue(getOperationsFromDatabaseResult(result));
					return actions;
				}
			}
			connection.close();
		} catch (Exception e) {
			e.printStackTrace();
		}
		return null;
	}

	private static int getLastSavedVersion(Connection connection, String roomName) throws SQLException {
		String tableName = "de_version_info";
		String query = "SELECT lastSavedVersion FROM \"" + tableName + "\" WHERE roomName ='" + roomName + "'";
		PreparedStatement statement = connection.prepareStatement(query);
		ResultSet resultSet = statement.executeQuery();
		int lastSavedVersion = 0;
		if (resultSet.next()) {
			lastSavedVersion = resultSet.getInt("lastSavedVersion");
		}
		resultSet.close();
		statement.close();
		return lastSavedVersion;
	}

	private static void createRecordForVersionInfo(Connection connection, String roomName) throws SQLException {
		String tableName = "de_version_info";
		try {
			if (!tableExists(connection, tableName)) {
				// If record not present, create new table
				String queryString = "CREATE TABLE \"" + tableName + "\" (roomName TEXT, lastSavedVersion INTEGER)";
				PreparedStatement preparedStatement = connection.prepareStatement(queryString);
				preparedStatement.executeUpdate();
			}
			String query = "INSERT INTO \"" + tableName + "\" (roomName, lastSavedVersion) VALUES (?, ?)";
			PreparedStatement preparedStatement = connection.prepareStatement(query);
			preparedStatement.setString(1, roomName);
			// Set initial version to 0;
			preparedStatement.setInt(2, 0);
			preparedStatement.executeUpdate();
		} catch (ClassNotFoundException e) {
			e.printStackTrace();
		}
	}

	private static boolean tableExists(Connection connection, String roomName) throws ClassNotFoundException {
		try {
			String query = "SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '"
					+ roomName + "') THEN 1 ELSE 0 END;";
			PreparedStatement statement = connection.prepareStatement(query);
			// statement.setString(1, tableName);
			ResultSet resultSet = statement.executeQuery();
			if (resultSet.next()) {
				int result = resultSet.getInt(1);
				return result == 1;
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
		return false;
	}

	private static ArrayList<Map<String, Object>> getOperationsFromDatabaseResult(ResultSet reader)
			throws SQLException {
		ArrayList<Map<String, Object>> table = new ArrayList<>();
		while (reader.next()) {
			Map<String, Object> row = new HashMap<>();
			row.put("version", reader.getInt("version"));
			row.put("clientVersion", reader.getInt("clientVersion"));
			row.put("operation", reader.getString("operation")); // Add other columns as needed
			table.add(row);
		}
		return table;
	}

	private static ArrayList<ActionInfo> getOperationsQueue(ArrayList<Map<String, Object>> table)
			throws JsonMappingException, JsonProcessingException {
		ArrayList<ActionInfo> actions = new ArrayList<>();
		for (Map<String, Object> row : table) {
			ObjectMapper objectMapper = new ObjectMapper();
			String jsonString = (String) row.get("operation");
			// Deserialize JSON string to ActionInfo object
			ActionInfo action = objectMapper.readValue(jsonString, ActionInfo.class);
			action.setVersion(Integer.parseInt(row.get("version").toString()));
			action.setClientVersion(Integer.parseInt(row.get("clientVersion").toString()));
			actions.add(action);
		}
		return actions;
	}

	private static WordProcessorHelper getDocumentFromBucketS3(String documentId, String accessKey, String secretKey,
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
