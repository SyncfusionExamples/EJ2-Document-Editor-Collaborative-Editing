//package com.syncfusion.tomcat;
//
//import java.util.concurrent.CompletableFuture;
//import com.syncfusion.tomcat.SaveInfo;
//
//public interface IBackgroundQueue {
//	CompletableFuture<Void> queueBackgroundWorkItemAsync(SaveInfo workItem);
//
//	CompletableFuture<SaveInfo> dequeueAsync();
//}
//
////@Component
////abstract class BackgroundQueue implements IBackgroundQueue{
////	@Override
////	public void queueBackgroundWorkItemAsync(SaveInfo message){
////		try {
////	        BlockingQueue<SaveInfo> queue = new LinkedBlockingQueue<>();
////			queue .put(message); // Add workItem to the queue (blocks if queue is full)    
////	    } catch (InterruptedException e) {
////	        System.out.println(e);   
////	    }
////	}
////}
//
////@Component
////class BackgroundQueueImpl implements IBackgroundQueue {
////
////    public final BlockingQueue<SaveInfo> queue;
////    private final ExecutorService executorService;
////
////    @Autowired
////    QueuedHostedService queuedHostedService;
////    
////    private int capacity=1;
////    
////    public BackgroundQueueImpl() {
////        // Initialize the queue with a specified capacity
////        this.queue = new LinkedBlockingQueue<>(capacity);
////        this.executorService=Executors.newFixedThreadPool(4);
////    }
////
////    @Override
////    public CompletableFuture<Void> queueBackgroundWorkItemAsync(SaveInfo workItem) {
////        if (workItem == null) {
////            throw new IllegalArgumentException("workItem cannot be null");
////        }
////
////        return CompletableFuture.runAsync(() -> {
////            try {
////                queue.put(workItem); // Blocking operation if the queue is full
////                if (!queue.isEmpty() ) {
////                	if (queuedHostedService != null) {
////                        queuedHostedService.start(); // Start processing if queue is full
////                    } 
////                    synchronized (queue) {
////                        System.out.println("Queue contents:");
////                        for (SaveInfo item : queue) {
////                            System.out.println(item);
////                        }
////                    }
////                }
////            } catch (InterruptedException e) {
////                Thread.currentThread().interrupt(); // Restore interrupted status
////                throw new RuntimeException("Failed to enqueue work item", e);
////            }
////        },executorService);
////    }
////
////    @Override
////    public CompletableFuture<SaveInfo> dequeueAsync() {
////    	
////        return CompletableFuture.supplyAsync(() -> {
////            try {
////            	Thread.sleep(5000);
////                return queue.take(); // Blocking operation if the queue is empty
////            } catch (InterruptedException e) {
////            	e.printStackTrace();
//////                Thread.currentThread().interrupt(); // Restore interrupted status
//////                throw new RuntimeException("Failed to dequeue work item", e);
////            }
////			return null;
////        });
////    }
////    
////    public void shutDown() {
////    	executorService.shutdown();
////    }
////}
