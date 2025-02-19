package com.syncfusion.tomcat;

public class CollaborativeEditingHelper {
	
	// Maximum number of operation we can queue in single revision.
    // If we reach this limit, we will save the operations to source document.
	public static final int saveThreshold = 150;
	
	// Suffix key to store revision information in redis cache.
	public static final String versionInfoSuffix = "_version_info";
	
	// Suffix key to store version information in redis cache.
	public static final String revisionInfoSuffix = "_revision_info";
	
	// Suffix key to store user information in redis cache.
	public static final String userInfoSuffix = "_user_info";
	
	// Suffix key to store removed actions information in redis cache.
	public static final String actionsToRemoveSuffix = "_actions_to_remove";

	// Key to store room information with connection Id in redis cache.
	public static final String connectionIdRoomMappingKey = "ej_de_connection_id_room_mapping";
	
	public static final String insertScript = "-- Define keys for version, list, and revision \n"+
			"local versionKey = KEYS[1] \n"+
			"local listKey = KEYS[2] \n"+
			"local revisionKey = KEYS[3] \n"+
			"local updateKey = KEYS[4] \n"+
			"-- Define arguments: item to insert, client's version, and threshold for cache \n"+
			"local item = ARGV[1] \n"+
			"local clientVersion = tonumber(ARGV[2]) \n"+
			"local threshold = tonumber(ARGV[3]) \n"+
			"-- Increment the version for each operation \n"+
			"local version = redis.call('INCR', versionKey) \n"+
			"-- Retrieve the current revision, or initialize it if it doesn't exist \n"+
			"local revision = redis.call('GET', revisionKey) \n"+
			"if not revision then \n"+
				"redis.call('SET', revisionKey, '0') \n"+
				"revision = 0 \n"+
			"else \n"+
				"revision = tonumber(revision) \n"+
			"end \n"+
			"-- Calculate the effective version by multiplying revision by threshold \n"+
			"local effectiveVersion = revision * threshold \n"+
			"-- Adjust clientVersion based on effectiveVersion \n"+
			"clientVersion = clientVersion - effectiveVersion \n"+
			"-- Add the new item to the list and get the new length \n"+
			"local length = redis.call('RPUSH', listKey, item) \n"+
			"-- Retrieve operations since the client's version \n"+
			"local previousOps = redis.call('LRANGE', listKey, clientVersion, -1) \n"+
			"-- Define a limit for cache based on threshold \n"+
			"local cacheLimit = threshold * 2; \n"+
			"local elementToRemove = nil \n"+
			"-- If the length of the list reaches the cache limit, trim the list \n"+
			"if length % cacheLimit == 0 then \n"+
				"elementToRemove = redis.call('LRANGE', listKey, 0, threshold - 1) \n"+
				"redis.call('LTRIM', listKey, threshold, -1) \n"+
				"-- Increment the revision after trimming \n"+
				"redis.call('INCR', revisionKey) \n"+
				"-- Add elements to remove to updateKey \n"+
				"for _, v in ipairs(elementToRemove) do \n"+
					"redis.call('RPUSH', updateKey, v) \n"+
				"end \n"+
			"end \n"+
			"-- Return the current version, operations since client's version, and elements removed \n"+
			"local values = {version, previousOps, elementToRemove} \n"+
			"return values \n";

	public static final String updateRecord = "-- Define keys for list and revision \n" +
		"local listKey = KEYS[1] \n" +
		"local revisionKey = KEYS[2] \n"+
		"-- Define arguments: item to insert, client's version, and threshold for cache \n"+
		"local item = ARGV[1] \n"+
		"local clientVersion = ARGV[2] \n"+
		"local threshold = tonumber(ARGV[3]) \n"+   
		"-- Retrieve the current revision from Redis, or initialize it if it doesn't exist \n"+
		"local revision = redis.call('GET', revisionKey) \n"+
		"if not revision then \n"+
			"revision = 0 \n"+
		"else \n"+
			"revision = tonumber(revision) \n"+
		"end \n"+
		"-- Calculate the effective version by multiplying revision by threshold \n"+
		"local effectiveVersion = revision * threshold \n"+
		"-- Adjust clientVersion based on effectiveVersion \n"+
		"clientVersion = tonumber(clientVersion) - effectiveVersion \n"+
		"-- Update the list at the position calculated by the adjusted clientVersion \n"+
		"-- This effectively 'inserts' the item into the list at the position reflecting the client's view of the list \n"+
		"redis.call('LSET', listKey, clientVersion, item) \n";

	public static final String effectivePendingOperations = "-- Define the keys for accessing the list and revision in Redis \n"+
			"local listKey = KEYS[1] \n"+
			"local revisionKey = KEYS[2] \n"+
			"-- Convert the first argument to a number to represent the client's version \n"+
			"local clientVersion =  tonumber(ARGV[1]) \n"+
			"-- Convert the second argument to a number for the threshold value \n"+
			"local threshold = tonumber(ARGV[2]) \n"+
			"-- Retrieve the current revision number from Redis \n"+
			"local revision = redis.call('GET', revisionKey) \n"+
			"if not revision then \n"+
			"revision = 0 \n"+
			"else \n"+
			"revision = tonumber(revision) \n"+
			"end \n"+
			"-- Calculate the effective version by multiplying the revision number by the threshold \n"+
			"-- This helps in determining the actual version of the document considering the revisions \n"+
			"local effectiveVersion = revision * threshold \n"+
			"-- Adjust the client's version by subtracting the effective version \n"+
			"-- This calculation aligns the client's version with the server's version, accounting for any revisions \n"+
			"clientVersion = clientVersion - effectiveVersion \n"+
			"-- Return a range of list elements starting from the adjusted client version to the end of the list \n"+
			"-- This command retrieves all operations that have occurred since the client's last known state \n"+
			"if clientVersion >= 0 then \n"+
			"return redis.call('LRANGE', listKey, clientVersion, -1) \n"+
		"else \n"+
			"return {} \n"+
		"end \n";

	public static final String pendingOperations =	"local listKey = KEYS[1] \n"+
		"local processingKey = KEYS[2] \n"+
		"local startIndex = tonumber(ARGV[1]) \n"+
		"local endIndex = tonumber(ARGV[2]) \n"+
		"-- Fetch the list of operations from the listKey \n"+
		"local listValues = redis.call('LRANGE', listKey, startIndex, endIndex) \n"+
		"-- Fetch the list of operations from the processingKey \n"+
		"local processingValues = redis.call('LRANGE', processingKey, startIndex, endIndex) \n"+
		"-- Return both lists as a combined result \n"+
		"return {processingValues, listValues} \n";
}
