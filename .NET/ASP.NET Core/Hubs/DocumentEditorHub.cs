using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Syncfusion.EJ2.DocumentEditor;
using WebApplication1.Controllers;

namespace WebApplication1.Hubs
{
    public class DocumentEditorHub : Hub
    {

        static Dictionary<string, ActionInfo> userManager = new Dictionary<string, ActionInfo>();
        internal static Dictionary<string, List<ActionInfo>> groupManager = new Dictionary<string, List<ActionInfo>>();

        public async Task JoinGroup(ActionInfo info)
        {
            if (!userManager.ContainsKey(Context.ConnectionId))
            {
                userManager.Add(Context.ConnectionId, info);
            }
            info.ConnectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, info.RoomName);
            if (groupManager.ContainsKey(info.RoomName))
            {
                await Clients.Caller.SendAsync("dataReceived", "addUser", groupManager[info.RoomName]);
            }
            lock (groupManager)
            {
                if (groupManager.ContainsKey(info.RoomName))
                {
                    groupManager[info.RoomName].Add(info);
                }
                else
                {
                    List<ActionInfo> actions = new List<ActionInfo>
                    {
                        info
                    };
                    groupManager.Add(info.RoomName, actions);
                }
            }
            Clients.GroupExcept(info.RoomName, Context.ConnectionId).SendAsync("dataReceived", "addUser", info);
        }

        public override Task OnConnectedAsync()
        {
            Clients.Caller.SendAsync("dataReceived", "connectionId", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override System.Threading.Tasks.Task OnDisconnectedAsync(Exception? e)
        {
            string roomName = userManager[Context.ConnectionId].RoomName;
            if (groupManager.ContainsKey(roomName))
            {
                groupManager[roomName].Remove(userManager[Context.ConnectionId]);

                if (groupManager[roomName].Count == 0)
                {
                    groupManager.Remove(roomName);
                    string userid = Context.GetHttpContext().Session.GetString("UserId");
                    CollaborativeEditingController.UpdateOperationsToSourceDocument(roomName, userid, false);
                }
            }

            if (userManager.ContainsKey(Context.ConnectionId))
            {
                Clients.OthersInGroup(roomName).SendAsync("dataReceived", "removeUser", Context.ConnectionId);
                Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
                userManager.Remove(Context.ConnectionId);
            }
            return base.OnDisconnectedAsync(e);
        }
    }
}
