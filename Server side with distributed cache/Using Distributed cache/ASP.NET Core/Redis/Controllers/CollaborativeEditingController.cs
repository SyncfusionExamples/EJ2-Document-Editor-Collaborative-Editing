using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.DocumentEditor;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;
using System.Data;
using Microsoft.CodeAnalysis;
using StackExchange.Redis;
using Newtonsoft.Json;
using WebApplication1.Model;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CollaborativeEditingController : ControllerBase
    {
        private static string fileLocation;
        private IBackgroundTaskQueue saveTaskQueue;
        private static IConnectionMultiplexer _redisConnection;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IHubContext<DocumentEditorHub> _hubContext;
     
        // Constructor for the CollaborativeEditingController
        public CollaborativeEditingController(IWebHostEnvironment hostingEnvironment,
            IHubContext<DocumentEditorHub> hubContext,
            IConfiguration config, IConnectionMultiplexer redisConnection, IBackgroundTaskQueue taskQueue)
        {
            _hostingEnvironment = hostingEnvironment;
            _hubContext = hubContext;
            _redisConnection = redisConnection;
            fileLocation = _hostingEnvironment.WebRootPath;
            saveTaskQueue = taskQueue;
        }

        //Import document from wwwroot folder in web server.
        [HttpPost]
        [Route("ImportFile")]
        [EnableCors("AllowAllOrigins")]
        public async Task<string> ImportFile([FromBody] WebApplication1.Model.FileInfo param)
        {
            try
            {
                // Create a new instance of DocumentContent to hold the document data
                DocumentContent content = new DocumentContent();
                // Retrieve the source document to be edited
                // In this case, document is retrieved from the Getting Started.docx file in the wwwroot folder
                // We can modify the code to retrieve the document from a different location or source.
                Syncfusion.EJ2.DocumentEditor.WordDocument document = GetSourceDocument();
                // Get the list of pending operations for the document
                List<ActionInfo> actions = await GetPendingOperations(param.fileName, 0, -1);
                if (actions != null && actions.Count > 0)
                {
                    // If there are any pending actions, update the document with these actions
                    document.UpdateActions(actions);
                }
                // Serialize the updated document to SFDT format
                string sfdt = Newtonsoft.Json.JsonConvert.SerializeObject(document);
                content.version = 0;
                content.sfdt = sfdt;
                // Dispose of the document to free resources
                document.Dispose();
                // Return the serialized content as a JSON string
                return Newtonsoft.Json.JsonConvert.SerializeObject(content);
            }
            catch
            {
                return null;
            }
        }

        [HttpPost]
        [Route("UpdateAction")]
        [EnableCors("AllowAllOrigins")]
        public async Task<ActionInfo> UpdateAction(ActionInfo param)
        {
            ActionInfo modifiedAction = await AddOperationsToTable(param);
            await _hubContext.Clients.Group(param.RoomName).SendAsync("dataReceived", "action", modifiedAction);
            return modifiedAction;
        }


        [HttpPost]
        [Route("GetActionsFromServer")]
        [EnableCors("AllowAllOrigins")]
        public async Task<string> GetActionsFromServer(ActionInfo param)
        {
            try
            {
                // Initialize necessary variables from the parameters and helper class
                int saveThreshold = CollaborativeEditingHelper.SaveThreshold;
                string tableName = param.RoomName;
                int lastSyncedVersion = param.Version;
                int clientVersion = param.Version;

                // Retrieve the database connection
                IDatabase database = _redisConnection.GetDatabase();

                // Fetch actions that are effective and pending based on the last synced version
                List<ActionInfo> actions = await GetEffectivePendingVersion(tableName, lastSyncedVersion, database);

                // Increment the version for each action sequentially
                actions.ForEach(action => action.Version = ++clientVersion);

                // Filter actions to only include those that are newer than the client's last known version
                actions = actions.Where(action => action.Version > lastSyncedVersion).ToList();

                // Transform actions that have not been transformed yet
                actions.Where(action => !action.IsTransformed).ToList()
                    .ForEach(action => CollaborativeEditingHandler.TransformOperation(action, actions));

                // Serialize the filtered and transformed actions to JSON and return
                return Newtonsoft.Json.JsonConvert.SerializeObject(actions);
            }
            catch
            {
                // In case of an exception, return an empty JSON object
                return "{}";
            }
        }

        private async Task<ActionInfo> AddOperationsToTable(ActionInfo action)
        {
            int clientVersion = action.Version;

            // Initialize the database connection
            IDatabase database = _redisConnection.GetDatabase();
            // Define the keys for Redis operations based on the action's room name
            RedisKey[] keys = new RedisKey[] { action.RoomName + CollaborativeEditingHelper.VersionSuffix, action.RoomName, action.RoomName + CollaborativeEditingHelper.RevisionSuffix, action.RoomName + CollaborativeEditingHelper.ElementsToBeRemoved };
            // Serialize the action and prepare values for the Redis script
            RedisValue[] values = new RedisValue[] { JsonConvert.SerializeObject(action), clientVersion.ToString(), CollaborativeEditingHelper.SaveThreshold.ToString() };
            // Execute the Lua script in Redis and store the results
            RedisResult[] results = (RedisResult[])await database.ScriptEvaluateAsync(CollaborativeEditingHelper.InsertScript, keys, values);

            // Parse the version number from the script results
            int version = int.Parse(results[0].ToString());
            // Deserialize the list of previous operations from the script results
            List<ActionInfo> previousOperations = ((RedisResult[])results[1]).Select(value => JsonConvert.DeserializeObject<ActionInfo>(value.ToString())).ToList();

            // Increment the version for each previous operation
            previousOperations.ForEach(op => op.Version = ++clientVersion);

            // Check if there are multiple previous operations to determine if transformation is needed
            if (previousOperations.Count > 1)
            {
                // Set the current action to the last operation in the list
                action = previousOperations.Last();
                // Transform operations that have not been transformed yet
                previousOperations.Where(op => !op.IsTransformed).ToList().ForEach(op => CollaborativeEditingHandler.TransformOperation(op, previousOperations));
            }
            // Update the action's version and mark it as transformed
            action.Version = version;
            action.IsTransformed = true;
            // Update the record in the cache with the new version
            UpdateRecordToCache(version, action, database);

            // Check if there are cleared operations to be saved
            if (results.Length > 2 && !results[2].IsNull)
            {
                // Deserialize the cleared operations from the results
                RedisResult[] clearedOperation = (RedisResult[])results[2];
                List<ActionInfo> actions = new List<ActionInfo>();
                // Prepare the message fir adding it in background service queue.
                foreach (var element in clearedOperation)
                {
                    actions.Add(JsonConvert.DeserializeObject<ActionInfo>(element.ToString()));
                }
                var message = new SaveInfo
                {
                    Action = actions,
                    PartialSave = true,
                    RoomName = action.RoomName,
                };
                // Queue the message for background processing and save the operations to source document in background task
                _ = saveTaskQueue.QueueBackgroundWorkItemAsync(message);
            }
            // Return the updated action
            return action;
        }

        private async void UpdateRecordToCache(int version, ActionInfo action, IDatabase database)
        {
            // Prepare Redis keys for accessing the room and its revision information
            RedisKey[] keys = new RedisKey[]
            {
                action.RoomName, // Key for the room's main data
                action.RoomName + CollaborativeEditingHelper.RevisionSuffix // Key for the room's revision data
            };

            // Prepare Redis values for the script execution
            RedisValue[] values = new RedisValue[]
            {
                JsonConvert.SerializeObject(action), // Serialize the action to store/update it in Redis
                (version - 1).ToString(), // Decrement the version to get the previous version for comparison or update
                CollaborativeEditingHelper.SaveThreshold.ToString() // Convert the save threshold to string for Redis
            };

            // Execute the Lua script with the prepared keys and values
            // This script is likely updating the action in the room and possibly handling revision checks or updates
            await database.ScriptEvaluateAsync(CollaborativeEditingHelper.UpdateRecord, keys, values);

            //List<ActionInfo> cachedActions = await GetPendingOperations(action.RoomName, 0, -1);
            //Console.Write(cachedActions.Count);
        }

        private async Task<List<ActionInfo>> GetEffectivePendingVersion(string roomName, int startIndex, IDatabase databse)
        {

            // Define Redis keys for accessing the room data and its revision information
            RedisKey[] keys = new RedisKey[]
            {
                roomName, // Key for the room's actions
                roomName + CollaborativeEditingHelper.RevisionSuffix // Key for the room's revision data
            };

            // Prepare Redis values for the script: start index and save threshold
            RedisValue[] values = new RedisValue[]
            {
                startIndex.ToString(), // Convert start index to string for Redis command
                CollaborativeEditingHelper.SaveThreshold.ToString() // Convert save threshold to string for Redis command
            };

            // Execute the Lua script on Redis to fetch upcoming actions based on the provided keys and values
            RedisResult[] upcomingActions = (RedisResult[])await databse.ScriptEvaluateAsync(CollaborativeEditingHelper.EffectivePendingOperations, keys, values);

            // Deserialize the fetched actions from Redis and convert them into a list of ActionInfo objects
            return upcomingActions.Select(value => JsonConvert.DeserializeObject<ActionInfo>(value.ToString())).ToList();
        }

        // Method to retrieve pending operations from a Redis list between specified indexes
        public async Task<List<ActionInfo>> GetPendingOperations(string listKey, long startIndex, long endIndex)
        {
            // Get the database connection from the Redis connection multiplexer
            var db = _redisConnection.GetDatabase();
            var result = (RedisResult[])await db.ScriptEvaluateAsync(CollaborativeEditingHelper.PendingOperations, new RedisKey[] { listKey, listKey + CollaborativeEditingHelper.ElementsToBeRemoved }, new RedisValue[] { startIndex, endIndex });
            var processingValues = (RedisResult[])result[0];
            var listValues = (RedisResult[])result[1];

            // Initialize the list to hold ActionInfo objects
            var actionInfoList = new List<ActionInfo>();

            // Deserialize the operations from JSON to ActionInfo objects and store in the list
            actionInfoList.AddRange(processingValues.Select(value => Newtonsoft.Json.JsonConvert.DeserializeObject<ActionInfo>((string)value)));

            // Deserialize the operations from JSON to ActionInfo objects and return as a list
            actionInfoList.AddRange(listValues.Select(value => Newtonsoft.Json.JsonConvert.DeserializeObject<ActionInfo>((string)value)));

            return actionInfoList;
        }

        internal static Syncfusion.EJ2.DocumentEditor.WordDocument GetSourceDocument()
        {
            string path = fileLocation + "\\Getting Started.docx";
            int index = path.LastIndexOf('.');
            string type = index > -1 && index < path.Length - 1 ?
              path.Substring(index) : ".docx";
            Stream stream = System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Syncfusion.EJ2.DocumentEditor.WordDocument document = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(stream, GetFormatType(type));
            stream.Dispose();
            return document;
        }

        internal static FormatType GetFormatType(string format)
        {
            if (string.IsNullOrEmpty(format))
                throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            switch (format.ToLower())
            {
                case ".dotx":
                case ".docx":
                case ".docm":
                case ".dotm":
                    return FormatType.Docx;
                case ".dot":
                case ".doc":
                    return FormatType.Doc;
                case ".rtf":
                    return FormatType.Rtf;
                case ".txt":
                    return FormatType.Txt;
                case ".xml":
                    return FormatType.WordML;
                case ".html":
                    return FormatType.Html;
                default:
                    throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            }
        }
    }
}
