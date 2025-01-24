package com.syncfusion.tomcat;

import java.io.ByteArrayOutputStream;
import java.io.Console;
import java.io.FileOutputStream;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Semaphore;

import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;

import com.syncfusion.docio.WordDocument;
import com.syncfusion.ej2.wordprocessor.ActionInfo;
import com.syncfusion.ej2.wordprocessor.CollaborativeEditingHandler;
import com.syncfusion.ej2.wordprocessor.WordProcessorHelper;
import com.syncfusion.tomcat.controller.RedisSubscriber;

import ch.qos.logback.classic.Logger;
import redis.clients.jedis.Jedis;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.AwsCredentials;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.model.PutObjectRequest;
import software.amazon.awssdk.regions.Region;

@Service
public class BackgroundService {
	
	private static final Logger logger = (Logger) LoggerFactory.getLogger(BackgroundService.class);
	private final List<SaveInfo> itemsToProcess = new ArrayList<>();
	private final Semaphore semaphore = new Semaphore(1);

	@Value("${spring.datasource.accesskey}")
	private String datasourceAccessKey;
	@Value("${spring.datasource.secretkey}")
	private String datasourceSecretKey;
	@Value("${spring.datasource.bucketname}")
	private String datasourceBucketName;
	@Value("${spring.datasource.regionname}")
	private String datasourceRegionName;

	@Scheduled(fixedRate = 5000) // Runs every 10 seconds
	public void runBackgroundTask() {
		try {
			semaphore.acquire();
			synchronized (itemsToProcess) {
				while (!itemsToProcess.isEmpty()) {
					SaveInfo item = itemsToProcess.remove(0);
					logger.info("Processing item : ",item);
					// Process the item here
					applyOperationsToSourceDocument(item);
					clearRecordsFromRedisCache(item);
				}
			}
		} catch (InterruptedException e) {
			// Handle the exception if needed
		} finally {
			semaphore.release();
		}
	}

	public void addItemToProcess(SaveInfo item) {
		synchronized (itemsToProcess) {
			itemsToProcess.add(item);
		}
	}

	private void applyOperationsToSourceDocument(SaveInfo workItem) {
		try {
			ArrayList<ActionInfo> actions = (ArrayList<ActionInfo>) workItem.getActions();
			for (ActionInfo action : actions) {
				if (!action.isTransformed()) {
					CollaborativeEditingHandler.transformOperation(action, actions);
				}
			}
			ClassLoader classLoader = getClass().getClassLoader();
			String fileName = CollaborativeEditingController.documentName;
			WordProcessorHelper document = CollaborativeEditingController.getDocumentFromBucketS3(fileName,datasourceAccessKey,
			datasourceSecretKey, datasourceBucketName);
			CollaborativeEditingHandler handler = new CollaborativeEditingHandler(document);

			if (actions != null && actions.size() > 0) {
				for (ActionInfo info : actions) {
					handler.updateAction(info);
				}
				ByteArrayOutputStream outputStream = new ByteArrayOutputStream();
				WordDocument doc = WordProcessorHelper.save(WordProcessorHelper.serialize(handler.getDocument()));
				doc.save(outputStream, com.syncfusion.docio.FormatType.Docx);

				byte[] data = outputStream.toByteArray();

				AwsCredentials credentials = AwsBasicCredentials.create(datasourceAccessKey, datasourceSecretKey);
				StaticCredentialsProvider credentialsProvider = StaticCredentialsProvider.create(credentials);
				S3Client s3Client = S3Client.builder().region(Region.US_EAST_1).credentialsProvider(credentialsProvider)
						.build();
				PutObjectRequest objectRequest = PutObjectRequest.builder().bucket(datasourceBucketName).key(fileName).build();
				s3Client.putObject(objectRequest, software.amazon.awssdk.core.sync.RequestBody.fromBytes(data));
				s3Client.close();

				// String currentDir = System.getProperty("user.dir") + "/src/main/resources/static/files";
				// try (FileOutputStream fos = new FileOutputStream(currentDir + workItem.getRoomName())) {
				// 	// Write the byte array to the file
				// 	fos.write(data);
				// 	fos.close();
				// } catch (Exception ex) {
				// 	ex.printStackTrace();
				// }
				outputStream.close();
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	private void clearRecordsFromRedisCache(SaveInfo workItem) {
		Boolean partialSave = workItem.getPartialSave();
		String roomName = workItem.getRoomName();
		try (Jedis jedis = RedisSubscriber.getJedis()) {
			if (!partialSave) {
				jedis.del(roomName);
				jedis.del(roomName + CollaborativeEditingHelper.revisionInfoSuffix);
				jedis.del(roomName + CollaborativeEditingHelper.versionInfoSuffix);
			}
			jedis.del(roomName + CollaborativeEditingHelper.actionsToRemoveSuffix);
		}
	}
}