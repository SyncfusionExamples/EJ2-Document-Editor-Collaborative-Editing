using Syncfusion.EJ2.DocumentEditor;

namespace WebApplication1.Model
{
    public class CollaborativeEditingHelper
    {
        // Maximum number of operation we can queue in redis cache.
        // If we reach this limit, we will save the operations to source document.
        internal static int SaveThreshold = 100;

        // Suffix key to store revision information in redis cache.
        internal static string RevisionInfoSuffix = "_revision_info";

        // Suffix key to store version information in redis cache.
        internal static string VersionInfoSuffix = "_version_info";

        // Suffix key to store user information in redis cache.
        internal static string UserInfoSuffix = "_user_info";

        // Key to store room information with conncetion Id in redis cache.
        internal static string ConnectionIdRoomMappingKey = "ej_de_connection_id_room_mapping";

        // Suffix key to store removed actions information in redis cache.
        internal static string ActionsToRemoveSuffix = "_actions_to_remove";

        internal static string InsertScript = @"
                -- Define keys for version, list, and revision
                local versionKey = KEYS[1]
                local listKey = KEYS[2]
                local revisionKey = KEYS[3]
                local updateKey = KEYS[4]

                -- Define arguments: item to insert, client's version, and threshold for cache
                local item = ARGV[1]
                local clientVersion = tonumber(ARGV[2])
                local threshold = tonumber(ARGV[3])

                -- Increment the version for each operation
                local version = redis.call('INCR', versionKey)

                -- Retrieve the current revision, or initialize it if it doesn't exist
                local revision = redis.call('GET', revisionKey)
                if not revision then
                    redis.call('SET', revisionKey, '0')
                    revision = 0
                else
                    revision = tonumber(revision)
                end

                -- Calculate the effective version by multiplying revision by threshold
                local effectiveVersion = revision * threshold
                -- Adjust clientVersion based on effectiveVersion
                clientVersion = clientVersion - effectiveVersion

                -- Add the new item to the list and get the new length
                local length = redis.call('RPUSH', listKey, item)
                -- Retrieve operations since the client's version
                local previousOps = redis.call('LRANGE', listKey, clientVersion, -1)

                -- Define a limit for cache based on threshold
                local cacheLimit = threshold * 2;
                local elementToRemove = nil
                -- If the length of the list reaches the cache limit, trim the list
                if length % cacheLimit == 0 then
                    elementToRemove = redis.call('LRANGE', listKey, 0, threshold - 1)
                    redis.call('LTRIM', listKey, threshold, -1)
                    -- Increment the revision after trimming
                    redis.call('INCR', revisionKey)
                    -- Add elements to remove to updateKey
                    for _, v in ipairs(elementToRemove) do
                        redis.call('RPUSH', updateKey, v)
                    end
                end

                -- Return the current version, operations since client's version, and elements removed
                local values = {version, previousOps, elementToRemove}
                return values";

        internal static string UpdateRecord = @"
             -- Define keys for list and revision
            local listKey = KEYS[1]
            local revisionKey = KEYS[2]

            -- Define arguments: item to insert, client's version, and threshold for cache
            local item = ARGV[1]
            local clientVersion = ARGV[2]
            local threshold = tonumber(ARGV[3])     

            -- Retrieve the current revision from Redis, or initialize it if it doesn't exist
            local revision = redis.call('GET', revisionKey)
            if not revision then
                revision = 0
            else
                revision = tonumber(revision)
            end

            -- Calculate the effective version by multiplying revision by threshold
            local effectiveVersion = revision * threshold

            -- Adjust clientVersion based on effectiveVersion
            clientVersion = tonumber(clientVersion) - effectiveVersion

            -- Update the list at the position calculated by the adjusted clientVersion
            -- This effectively 'inserts' the item into the list at the position reflecting the client's view of the list
            redis.call('LSET', listKey, clientVersion, item)";

        internal static string EffectivePendingOperations = @"
             -- Define the keys for accessing the list and revision in Redis
             local listKey = KEYS[1]
             local revisionKey = KEYS[2]

             -- Convert the first argument to a number to represent the client's version
             local clientVersion =  tonumber(ARGV[1])

             -- Convert the second argument to a number for the threshold value
             local threshold = tonumber(ARGV[2])     

             -- Retrieve the current revision number from Redis
             local revision = redis.call('GET', revisionKey)
             if not revision then
                revision = 0
             else
                revision = tonumber(revision)
             end
             -- Calculate the effective version by multiplying the revision number by the threshold
             -- This helps in determining the actual version of the document considering the revisions
             local effectiveVersion = revision * threshold


             -- Adjust the client's version by subtracting the effective version
             -- This calculation aligns the client's version with the server's version, accounting for any revisions
             clientVersion = clientVersion - effectiveVersion

             -- Return a range of list elements starting from the adjusted client version to the end of the list
             -- This command retrieves all operations that have occurred since the client's last known state
             if clientVersion >= 0 then
                return redis.call('LRANGE', listKey, clientVersion, -1)
            else
                return {}
            end";

        internal static string PendingOperations = @"
            local listKey = KEYS[1]
            local processingKey = KEYS[2]
            local startIndex = tonumber(ARGV[1])
            local endIndex = tonumber(ARGV[2])

            -- Fetch the list of operations from the listKey
            local listValues = redis.call('LRANGE', listKey, startIndex, endIndex)

            -- Fetch the list of operations from the processingKey
            local processingValues = redis.call('LRANGE', processingKey, startIndex, endIndex)

            -- Return both lists as a combined result
            return {processingValues, listValues}";
    }

    public class SaveInfo
    {
        public string RoomName { get; set; }
        public List<ActionInfo> Action;
        public string UserId;
        public int Version;
        public bool PartialSave;
    }

    public class FileInfo
    {
        public string fileName
        {
            get;
            set;
        }
        public string documentOwner
        {
            get;
            set;
        }
    }

    public class DocumentContent
    {
        public int version { get; set; }

        public string sfdt { get; set; }

    }


}
