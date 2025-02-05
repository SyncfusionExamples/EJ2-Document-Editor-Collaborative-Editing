//package com.syncfusion.tomcat;
//
//import java.io.ByteArrayOutputStream;
//import java.io.FileOutputStream;
//import java.util.ArrayList;
//import java.util.List;
//import java.util.concurrent.CompletableFuture;
//import java.util.concurrent.ExecutorService;
//import java.util.concurrent.Executors;
//import java.util.concurrent.atomic.AtomicBoolean;
//
//import org.springframework.beans.factory.annotation.Autowired;
//import org.springframework.scheduling.annotation.Async;
//import org.springframework.stereotype.Service;
//
//import com.syncfusion.docio.WordDocument;
//import com.syncfusion.ej2.wordprocessor.ActionInfo;
//import com.syncfusion.ej2.wordprocessor.CollaborativeEditingHandler;
//import com.syncfusion.ej2.wordprocessor.WordProcessorHelper;
//import com.syncfusion.tomcat.CollaborativeEditingHelper.SaveInfo;
//import com.syncfusion.tomcat.controller.RedisSubscriber;
//
//import redis.clients.jedis.Jedis;
//
//@Service
//public class QueuedHostedService implements IQueuedHostedService {
//	
//	
//	private IBackgroundQueue queue;
//	private static boolean isRunning=false;
//	private final ExecutorService executorService=Executors.newSingleThreadExecutor();
//	
//	@Autowired
//	public QueuedHostedService(IBackgroundQueue iqueue) {
//		queue=iqueue;
//		
//	}
//	
//	public  void start() {
//		if(isRunning) {
//			throw new IllegalStateException("Service is already running");
//		}
//		try {
//			executorService.submit(()->backgroundProcessing());
//			isRunning=true;
//		}
//		catch(Exception ex) {
//			ex.printStackTrace();
//		}
//	}
//	
//	public void stop() {
//		if(isRunning) {
//			executorService.shutdown();
//			isRunning=false;
//		}
//	}
//	
//	@Async
//	public  void backgroundProcessing() {	
//		while(true) {
//			try {
//				SaveInfo workItem=queue.dequeueAsync().get();
//				applyOperationsToSourceDocument(workItem);
//				clearRecordsFromRedisCache(workItem);
//			}
//			catch (Exception ex){
//				ex.printStackTrace();
//			}
//		}
//		
//	}
//	
//	private  void applyOperationsToSourceDocument(SaveInfo workItem) {
//		ClassLoader classLoader = getClass().getClassLoader();
//		try {
//			WordProcessorHelper document = WordProcessorHelper
//					.load(classLoader.getResourceAsStream("static/files/" + workItem.getRoomName()), true);
//			CollaborativeEditingHandler handler= new CollaborativeEditingHandler(document);
//			
//			ArrayList<ActionInfo> actions = (ArrayList<ActionInfo>) workItem.getAction();
//			if(actions != null && actions.size()>0) {
//				for(ActionInfo action : actions){
//					if(!action.isTransformed()) {
//						CollaborativeEditingHandler.transformOperation(action, actions);
//					}
//				}
//				
//				for(int i=0;i<actions.size();i++) {
//					handler.updateAction(actions.get(i));
//				}
//				ByteArrayOutputStream outputStream = new ByteArrayOutputStream();
//				WordDocument doc = WordProcessorHelper.save(WordProcessorHelper.serialize(handler.getDocument()));
//				doc.save(outputStream, com.syncfusion.docio.FormatType.Docx);
//				
//				byte[] data = outputStream.toByteArray();
//				
//				try (FileOutputStream fos = new FileOutputStream("static/files/" + workItem.getRoomName())) {
//					// Write the byte array to the file
//					fos.write(data);
//					fos.close();
//				} catch (Exception ex) {
//					ex.printStackTrace();
//				}
//				outputStream.close();
//			}
//			
//		} catch (Exception e) {
//			// TODO Auto-generated catch block
//			e.printStackTrace();
//		}
//	}
//	
//	private  void clearRecordsFromRedisCache(SaveInfo workItem) {
//		Boolean partialSave = workItem.getPartialSave();
//		String roomName=workItem.getRoomName();
//		try(Jedis jedis = RedisSubscriber.getJedis()){
//			if(!partialSave) {
//				jedis.del(roomName);
//				jedis.del(roomName+CollaborativeEditingHelper.getRevisionSuffix());
//				jedis.del(roomName+CollaborativeEditingHelper.getVersionSuffix());
//			}
//			jedis.del(roomName+CollaborativeEditingHelper.getActionInfoSuffix());
//		}
//	}
//
//}
