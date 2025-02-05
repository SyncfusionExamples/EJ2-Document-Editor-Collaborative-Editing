//package com.syncfusion.tomcat;
//
//import java.io.ByteArrayOutputStream;
//import java.io.FileOutputStream;
//import java.util.ArrayList;
//import java.util.concurrent.BlockingQueue;
//import java.util.concurrent.CompletableFuture;
//import java.util.concurrent.ExecutorService;
//import java.util.concurrent.Executors;
//import java.util.concurrent.LinkedBlockingQueue;
//
//import org.springframework.beans.factory.annotation.Autowired;
//import org.springframework.context.annotation.Lazy;
//import org.springframework.scheduling.annotation.Async;
//import org.springframework.stereotype.Component;
//
//import com.syncfusion.docio.WordDocument;
//import com.syncfusion.ej2.wordprocessor.ActionInfo;
//import com.syncfusion.ej2.wordprocessor.CollaborativeEditingHandler;
//import com.syncfusion.ej2.wordprocessor.WordProcessorHelper;
//import com.syncfusion.tomcat.SaveInfo;
//import com.syncfusion.tomcat.controller.RedisSubscriber;
//
//import redis.clients.jedis.Jedis;
//
//@Component
//class BackgroundQueueImpl implements IBackgroundQueue {
//
//	public final BlockingQueue<SaveInfo> queue;
//	private final ExecutorService executorService;
//	private static boolean isRunning = false;
////    private int capacity=1;
//
//	public BackgroundQueueImpl() {
//		// Initialize the queue with a specified capacity
//		this.queue = new LinkedBlockingQueue<>();
//		this.executorService = Executors.newFixedThreadPool(4);
//	}
//
//	@Override
//	public CompletableFuture<Void> queueBackgroundWorkItemAsync(SaveInfo workItem) {
//		if (workItem == null) {
//			throw new IllegalArgumentException("workItem cannot be null");
//		}
//
//		return CompletableFuture.runAsync(() -> {
//			try {
//				queue.put(workItem); // Blocking operation if the queue is full
//				if (!queue.isEmpty()) {
//					start(); // Start processing if queue contains element
//				}
//			} catch (InterruptedException e) {
//				Thread.currentThread().interrupt(); // Restore interrupted status
//				throw new RuntimeException("Failed to enqueue work item", e);
//			}
//		}, executorService);
//	}
//
//	@Override
//	public CompletableFuture<SaveInfo> dequeueAsync() {
//
//		return CompletableFuture.supplyAsync(() -> {
//			try {
//				Thread.sleep(5000);
//				return queue.take(); // Blocking operation if the queue is empty
//			} catch (InterruptedException e) {
//				e.printStackTrace();
//			}
//			return null;
//		});
//	}
//
//	public void start() {
//		if (isRunning) {
//			throw new IllegalStateException("Service is already running");
//		}
//		try {
//			executorService.submit(() -> backgroundProcessing());
//			isRunning = true;
//		} catch (Exception ex) {
//			ex.printStackTrace();
//		}
//	}
//
//	@Async
//	public void backgroundProcessing() {
//		while (true) {
//			try {
//				SaveInfo workItem = dequeueAsync().get();
//				applyOperationsToSourceDocument(workItem);
//				clearRecordsFromRedisCache(workItem);
//			} catch (Exception ex) {
//				ex.printStackTrace();
//			}
//		}
//
//	}
//
//	private void applyOperationsToSourceDocument(SaveInfo workItem) {
//
//		try {
//			ArrayList<ActionInfo> actions = (ArrayList<ActionInfo>) workItem.getActions();
//			for (ActionInfo action : actions) {
//				if (!action.isTransformed()) {
//					CollaborativeEditingHandler.transformOperation(action, actions);
//				}
//			}
//
//			ClassLoader classLoader = getClass().getClassLoader();
//			WordProcessorHelper document = WordProcessorHelper
//					.load(classLoader.getResourceAsStream("static/files/" + workItem.getRoomName()), true);
//			CollaborativeEditingHandler handler = new CollaborativeEditingHandler(document);
//
//			if (actions != null && actions.size() > 0) {
//				for (ActionInfo info : actions) {
//					handler.updateAction(info);
//				}
//				ByteArrayOutputStream outputStream = new ByteArrayOutputStream();
//				WordDocument doc = WordProcessorHelper.save(WordProcessorHelper.serialize(handler.getDocument()));
//				doc.save(outputStream, com.syncfusion.docio.FormatType.Docx);
//
//				byte[] data = outputStream.toByteArray();
//
//				String currentDir = System.getProperty("user.dir") + "/src/main/resources/static/files";
//				try (FileOutputStream fos = new FileOutputStream(currentDir + workItem.getRoomName())) {
//					// Write the byte array to the file
//					fos.write(data);
//					fos.close();
//				} catch (Exception ex) {
//					ex.printStackTrace();
//				}
//				outputStream.close();
//			}
//		} catch (Exception e) {
//			// TODO Auto-generated catch block
//			e.printStackTrace();
//		}
//	}
//
//	private void clearRecordsFromRedisCache(SaveInfo workItem) {
//		Boolean partialSave = workItem.getPartialSave();
//		String roomName = workItem.getRoomName();
//		try (Jedis jedis = RedisSubscriber.getJedis()) {
//			if (!partialSave) {
//				jedis.del(roomName);
//				jedis.del(roomName + CollaborativeEditingHelper.revisionInfoSuffix);
//				jedis.del(roomName + CollaborativeEditingHelper.versionInfoSuffix);
//			}
//			jedis.del(roomName + CollaborativeEditingHelper.actionsToRemoveSuffix);
//		}
//	}
//
//	public void stop() {
//		if (isRunning) {
//			executorService.shutdown();
//			isRunning = false;
//		}
//	}
//
//	public void shutDown() {
//		executorService.shutdown();
//	}
//}
