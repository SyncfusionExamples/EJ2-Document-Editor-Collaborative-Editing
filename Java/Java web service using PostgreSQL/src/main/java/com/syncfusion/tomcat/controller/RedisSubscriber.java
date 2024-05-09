package com.syncfusion.tomcat.controller;

import java.util.HashMap;
import java.util.HashSet;
import java.util.Set;

import javax.annotation.PostConstruct;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
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
	

	@PostConstruct
	public void subscribeToInstanceChannel() {
		String channel = "collaborativedtiting";
		new Thread(() -> {
			JedisPoolConfig poolConfig = new JedisPoolConfig();
			jedisPool = new JedisPool(poolConfig, REDIS_HOST, REDIS_PORT);
			try (Jedis jedis = jedisPool.getResource()) {				
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
				System.out.println("Connection failed. Retrying ...");
			}
		}).start();
	}
}
