using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using StackExchange.Redis;
using Syncfusion.EJ2.DocumentEditor;
using WebApplication1.Model;
using WebApplication1.Service;

namespace WebApplication1.Hubs
{
    public class DocumentEditorHub : Hub
    {
        private readonly IDatabase _db;
        private IBackgroundTaskQueue saveTaskQueue;

        public DocumentEditorHub(IConnectionMultiplexer redisConnection, IBackgroundTaskQueue taskQueue)
        {
            _db = redisConnection.GetDatabase();
            saveTaskQueue = taskQueue;
        }
        public override Task OnConnectedAsync()
        {
            // Send session id to client.
            Clients.Caller.SendAsync("dataReceived", "connectionId", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public async Task JoinGroup(ActionInfo info)
        {
            // Set the connection ID to info
            info.ConnectionId = Context.ConnectionId;
            // Add the connection ID to the group
            await Groups.AddToGroupAsync(Context.ConnectionId, info.RoomName);

            //To ensure whether the room exixts in the Redis cache
            bool roomExists = await _db.KeyExistsAsync(info.RoomName + CollaborativeEditingHelper.UserInfoSuffix);
            if (roomExists)
            {
                // Fetch all connected users from Redis
                var allUsers = await _db.HashGetAllAsync(info.RoomName + CollaborativeEditingHelper.UserInfoSuffix);
                var userList = allUsers.Select(u => JsonConvert.DeserializeObject<ActionInfo>(u.Value)).ToList();

                //Send the exisiting user details to the newly joined user. 
                await Clients.Caller.SendAsync("dataReceived", "addUser", userList);
            }

            // Add user to Redis           
            await _db.HashSetAsync(info.RoomName + CollaborativeEditingHelper.UserInfoSuffix, Context.ConnectionId, JsonConvert.SerializeObject(info));

            // Store the room name with the connection ID
            await _db.HashSetAsync(CollaborativeEditingHelper.ConnectionIdRoomMappingKey, Context.ConnectionId, info.RoomName);

            // Notify all the exsisiting users in the group about the new user
            await Clients.GroupExcept(info.RoomName, Context.ConnectionId).SendAsync("dataReceived", "addUser", info);
        }
        public override async Task OnDisconnectedAsync(Exception? e)
        {
            //Get the room name associated with the connection ID
            string roomName = await _db.HashGetAsync(CollaborativeEditingHelper.ConnectionIdRoomMappingKey, Context.ConnectionId);
            //  Remove user from Redis       
            await _db.HashDeleteAsync(roomName + CollaborativeEditingHelper.UserInfoSuffix, Context.ConnectionId);

            //// Fetch all connected users from Redis
            var allUsers = await _db.HashGetAllAsync(roomName + CollaborativeEditingHelper.UserInfoSuffix);

            var userList = allUsers.Select(u => JsonConvert.DeserializeObject<ActionInfo>(u.Value)).ToList();

            // Remove connection to room name mapping
            await _db.HashDeleteAsync(CollaborativeEditingHelper.ConnectionIdRoomMappingKey, Context.ConnectionId);


            if (userList.Count == 0)
            {
                // Auto save the pending operations to source document
                RedisValue[] pendingOps = await _db.ListRangeAsync(roomName, 0, -1);
                if (pendingOps.Length > 0)
                {
                    List<ActionInfo> actions = new List<ActionInfo>();
                    // Prepare the message fir adding it in background service queue.
                    foreach (var element in pendingOps)
                    {
                        actions.Add(JsonConvert.DeserializeObject<ActionInfo>(element.ToString()));
                    }
                    var message = new SaveInfo
                    {
                        Action = actions,
                        PartialSave = false,
                        RoomName = roomName,
                    };
                    // Queue the message for background processing and save the operations to source document in background task
                    _ = saveTaskQueue.QueueBackgroundWorkItemAsync(message);
                }
            }
            else
            {
                // Notify remaining clients about the user disconnection              
                await Clients.Group(roomName).SendAsync("dataReceived", "removeUser", Context.ConnectionId);
            }
            await base.OnDisconnectedAsync(e);
        }
    }
}
