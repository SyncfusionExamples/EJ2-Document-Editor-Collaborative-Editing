package com.syncfusion.tomcat.controller;

import java.util.HashMap;
import java.util.HashSet;
import java.util.Set;

import javax.annotation.PostConstruct;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.messaging.MessageHeaders;
import org.springframework.stereotype.Component;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.syncfusion.ej2.wordprocessor.ActionInfo;

import redis.clients.jedis.HostAndPort;
import redis.clients.jedis.Jedis;
import redis.clients.jedis.JedisPool;
import redis.clients.jedis.JedisPoolConfig;
import redis.clients.jedis.JedisPubSub;
import redis.clients.jedis.exceptions.JedisConnectionException;

@Component
public class RedisSubscriber {
		public static JedisPool jedisPool;
		
		
		// Redis Configuration
		@Value("${spring.datasource.redishost}")
		private String REDIS_HOST;
		@Value("${spring.datasource.redisport}")
		private int REDIS_PORT;
		@Value("${spring.datasource.redispassword}")
		private String REDIS_PASSWORD;
		@Value("${spring.datasource.redisssl}")
		private boolean REDISSSL;

	@Autowired
	@PostConstruct
	@Bean
	public void subscribeToInstanceChannel() {
		String channel = "collaborativedtiting";
		new Thread(() -> {
			JedisPoolConfig poolConfig = new JedisPoolConfig();
			poolConfig.setMaxTotal(100);
			poolConfig.setMaxIdle(20);
			poolConfig.setMinIdle(10);
			poolConfig.setTestWhileIdle(true);
			jedisPool = new JedisPool(poolConfig, REDIS_HOST, REDIS_PORT,50000,REDIS_PASSWORD,REDISSSL);
			try (Jedis jedis = RedisSubscriber.getJedis()) {				
				jedis.subscribe(new JedisPubSub() {
					@Override
					public void onMessage(String channel, String message) {						
						System.out.println("Received message from channel " + channel + ": " + message);
						ObjectMapper objectMapper = new ObjectMapper();
						try {
							ActionInfo action = objectMapper.readValue(message, ActionInfo.class);
							HashMap<String, Object> updateAction = new HashMap<>();
							updateAction.put("action", "updateAction");
							MessageHeaders updateActionheaders = new MessageHeaders(updateAction);
							DocumentEditorHub.broadcastToRoom(action.getRoomName(), action, updateActionheaders);
						} catch (JsonProcessingException e) {
							e.printStackTrace();
						}
					}
					@Override
					public void onSubscribe(String channel, int subscribedChannels) {
						System.out.println("Subscribed to channel: " + channel);
					}
					
				}, channel);
				
			} catch (JedisConnectionException e) {
				// Handle the connection exception
				System.out.println("Connection failed. Retrying .....");
				e.printStackTrace();
			}
			 finally {
		            if (jedisPool != null) {
		                jedisPool.close(); // Ensure pool is closed when done
		            }
		        }
		}).start();
	}

	public static Jedis getJedis() {
		// TODO Auto-generated method stub
		return jedisPool.getResource();
	}
}
