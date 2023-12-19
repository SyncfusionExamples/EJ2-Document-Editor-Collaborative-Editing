# Collaborative Editing (preview)

Allows multiple users to work on the same document simultaneously. This can be done in real-time, so that collaborators can see the changes as they are made. Collaborative editing can be a great way to improve efficiency, as it allows team members to work together on a document without having to wait for others to finish their changes.

> Note: Collaborative editing support is currently in preview mode only and is not yet ready for production environments.

## Prerequisites

Following things are needed to enable collaborative editing in Document Editor

- SignalR
- Microsoft SQL Server

## How to enable collaborative editing in client side

### Step 1: Enable collaborative editing in Document Editor

To enable collaborative editing, inject `CollaborativeEditingHandler` and set the property `enableCollaborativeEditing` to true in the Document Editor, like in the code snippet below.

```typescript
import { DocumentEditorContainerComponent, CollaborativeEditingHandler, DocumentEditorComponent } from '@syncfusion/ej2-react-documenteditor';

public collaborativeEditingHandler?: CollaborativeEditingHandler;

// Inject collaborative editing module.
DocumentEditor.Inject(CollaborativeEditingHandler);

public componentDidMount(): void {
    if (this.container) {
        this.container.documentEditor.enableCollaborativeEditing = true;
        this.collaborativeEditingHandler = this.container.documentEditor.collaborativeEditingHandlerModule;
    }
    if (!this.connection) {
        this.initializeSignalR();
        this.loadDocumentFromServer();
    }
}

render() {
    return (<div className='control-pane'>
        <div>
            <div id='documenteditor_titlebar' className="e-de-ctn-title"></div>
            <div id="documenteditor_container_body">
                <DocumentEditorContainerComponent id="container" ref={(scope) => { this.container = scope; }} style={{ 'display': 'block' }}
                    height={'590px'} currentUser={this.currentUser} serviceUrl={this.serviceUrl + 'api/documenteditor'} enableToolbar={true} locale='en-US' >
                    <Inject services={[Toolbar]} />
                </DocumentEditorContainerComponent>
            </div>
        </div>
    </div>);
}

```

## Step 2: Configure SignalR to send and receive changes

To broadcast the changes made and receive changes from remote users, configure SignalR like below.

```typescript
import { HubConnectionBuilder, HttpTransportType, HubConnectionState, HubConnection } from '@microsoft/signalr';

public connectionId: string = '';
public connection?: HubConnection;

public initializeSignalR = (): void => {
    // SignalR connection
    this.connection = new HubConnectionBuilder().withUrl(this.serviceUrl + 'documenteditorhub', {
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
    }).withAutomaticReconnect().build();
    // Event handler for signalR connection
    this.connection.on('dataReceived', this.onDataRecived.bind(this));

    this.connection.onclose(async () => {
        if (this.connection && this.connection.state === HubConnectionState.Disconnected) {
            alert('Connection lost. Please relod the browser to continue.');
        }
    });
}

public onDataRecived(action: string, data: any) {
    if (this.collaborativeEditingHandler) {
        debugger;
        if (action == 'connectionId') {
            // Update the current connection id to track other users
            this.connectionId = data;
        } else if (this.connectionId != data.connectionId) {
            if (this.titleBar) {
                if (action == 'action' || action == 'addUser') {
                    // Add the user to title bar when user joins the room
                    this.titleBar.addUser(data);
                } else if (action == 'removeUser') {
                    // Remove the user from title bar when user leaves the room
                    this.titleBar.removeUser(data);
                }
            }
        }
        // Apply the remote action in DocumentEditor
        this.collaborativeEditingHandler.applyRemoteAction(action, data);
    }
}

public connectToRoom(data: any) {
    try {
        if (this.connection) {
            // start the connection.
            this.connection.start().then(() => {
                // Join the room.
                if (this.connection) {
                    this.connection.send('JoinGroup', { roomName: data.roomName, currentUser: data.currentUser });
                }
                console.log('server connected!!!');
            });
        }
    } catch (err) {
        console.log(err);
        // Attempting to reconnect in 5 seconds
        setTimeout(this.connectToRoom, 5000);
    }
};
```

### Step 3: Join SignalR room while opening the document

When opening a document, we need to generate a unique ID for each document. These unique IDs are then used to create rooms using SignalR, which facilitates sending and receiving data from the server.

```typescript
public openDocument(responseText: string, roomName: string): void {
    showSpinner(document.getElementById('container') as HTMLElement);
    let data = JSON.parse(responseText);
    if (this.container) {
        this.collaborativeEditingHandler = this.container.documentEditor.collaborativeEditingHandlerModule;
        // Update the room and version information to collaborative editing handler.
        this.collaborativeEditingHandler.updateRoomInfo(roomName, data.version, this.serviceUrl + 'api/CollaborativeEditing/');

        // Open the document
        this.container.documentEditor.open(data.sfdt);

        setTimeout(() => {
            if (this.container) {
                // connect to server using signalR
                this.connectToRoom({ action: 'connect', roomName: roomName, currentUser: this.container.currentUser });
            }
        });
    }
    hideSpinner(document.getElementById('container') as HTMLElement);
}
```

### Step 5: Broadcast current editing changes to remote users

Changes made on the client-side need to be sent to the server-side to broadcast them to other connected users. To send the changes made to the server, use the method shown below from the document editor using the `contentChange` event.

```typescript
    this.container.contentChange = (args: ContainerContentChangeEventArgs) => {
		if (this.collaborativeEditingHandler) {
			// Send the editing action to server
			this.collaborativeEditingHandler.sendActionToServer(args.operations as Operation[])
		}
	}
```

## How to enable collaborative editing in ASP.NET Core

### Step 1: Configure SignalR in ASP.NET Core 

We are using Microsoft SignalR to broadcast the changes. Please add the following configuration to your application's Program.cs file.

```csharp
    using Microsoft.Azure.SignalR;
    .....
    builder.Services.AddSignalR();
    .....
    .....
    .....
    app.MapHub<DocumentEditorHub>("/documenteditorhub");
    .....
    .....
```

### Step 2: Configure SignalR hub to create room for collaborative editing session

To manage groups for each document, create a folder named "Hub" and add a file named "DocumentEditorHub.cs" inside it. Add the following code to the file to manage SignalR groups using room names.

Join the group by using unique id of the document by using `JoinGroup` method.

```csharp
static Dictionary<string, ActionInfo> userManager = new Dictionary<string, ActionInfo>();
    internal static Dictionary<string, List<ActionInfo>> groupManager = new Dictionary<string, List<ActionInfo>>();

    // Join to the specified room name
    public async Task JoinGroup(ActionInfo info)
    {
        if (!userManager.ContainsKey(Context.ConnectionId))
        {
            userManager.Add(Context.ConnectionId, info);
        }
        info.ConnectionId = Context.ConnectionId;
        //Add the current connected use to the specified group
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
       // Notify other users in the group about new user joined the collaborative editing session.
        Clients.GroupExcept(info.RoomName, Context.ConnectionId).SendAsync("dataReceived", "addUser", info);
    }

```

Handle user disconnection using SignalR.

```csharp
//Handle disconnection from group.
    public override Task OnDisconnectedAsync(Exception? e)
    {
        string roomName = userManager[Context.ConnectionId].RoomName;
        if (groupManager.ContainsKey(roomName))
        {
            groupManager[roomName].Remove(userManager[Context.ConnectionId]);

            if (groupManager[roomName].Count == 0)
            {
                groupManager.Remove(roomName);
//If all user disconnected from current room. Auto save the change to source document.
CollaborativeEditingController.UpdateOperationsToSourceDocument(roomName, “<<documentpath>>”, false);
            }
        }

        if (userManager.ContainsKey(Context.ConnectionId))
        {
            //Notify other user in the group about user exit the collaborative editing session
            Clients.OthersInGroup(roomName).SendAsync("dataReceived", "removeUser", Context.ConnectionId);
            Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            userManager.Remove(Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(e);
    }

```

### Step 3: Configure Microsoft SQL database connection string in application level

Configure the SQL database that stores temporary data for the collaborative editing session. Provide the SQL database connection string in `appsettings.json` file.

```json
.....
"ConnectionStrings": {
  "DocumentEditorDatabase": "<SQL server connection string>"
}
.....

```

### Step 4: Configure Web API actions for collaborative editing

#### Import File

1.	When opening a document, create a database table to store temporary data for the collaborative editing session.
2.	If the table already exists, retrieve the records from the table and apply them to the WordDocument instance using the `UpdateActions` method before converting it to the SFDT format.

```csharp
public string ImportFile([FromBody] FileInfo param)
 {
     .....
     .....
     DocumentContent content = new DocumentContent();

     .....
     //Get source document from database/file system/blob storage
     WordDocument document = GetDocumentFromDatabase(param.fileName, param.documentOwner);
     .....
     //Get temporary records from database 
     List<ActionInfo> actions = CreatedTable(param.fileName);
     if(actions!=null)
     {
         //Apply temporary data to the document.
         document.UpdateActions(actions);
     }
     string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
     content.version = 0;
     content.sfdt = json;
     return Newtonsoft.Json.JsonConvert.SerializeObject(content);
 }

```

#### Update editing records to database.

Each edit operation made by the user is sent to the server and is pushed to the database. Each operation receives a version number after being inserted into the database.
After inserting the records to the server, the position of the current editing operation must be transformed against any previous editing operations not yet synced with the client using the TransformOperation method.
After performing the transformation, the current operation is broadcast to all connected users within the group.

```csharp
public async Task<ActionInfo> UpdateAction([FromBody] ActionInfo param)
{
    try
    {
        ActionInfo modifiedAction = AddOperationsToTable(param);
        //After transformation broadcast changes to all users in the gropu
        await _hubContext.Clients.Group(param.RoomName).SendAsync("dataReceived", "action", modifiedAction);
        return modifiedAction;
    }
    catch
    {
        return null;
    }
}

private ActionInfo AddOperationsToTable(ActionInfo action)
 {
     int clientVersion = action.Version;
     string tableName = action.RoomName;
     …………
     …………
     …………
     ………… 
     List<ActionInfo> actions = GetOperationsQueue(table);
     foreach (ActionInfo info in actions)
     {
      if (!info.IsTransformed)
      {
        CollaborativeEditingHandler.TransformOperation(info, actions);
      }
     }
     action = actions[actions.Count - 1];
     action.Version = updateVersion;
     //Return the transformed operation to broadcast it to other clients.
     return action;
 }

```

#### Add Web API to get previous operation as a backup to get lost operations

On the client side, messages broadcasted using SignalR may be received in a different order, or some operations may be missed due to network issues. In these cases, we need a backup method to retrieve missing records from the database.
Using the following method, we can retrieve all operations after the last successful client-synced version and return all missing operations to the requesting client.

```csharp
public async Task<ActionInfo> UpdateAction([FromBody] ActionInfo param)
{
    try
    {
        ActionInfo modifiedAction = AddOperationsToTable(param);
        //After transformation broadcast changes to all users in the gropu
        await _hubContext.Clients.Group(param.RoomName).SendAsync("dataReceived", "action", modifiedAction);
        return modifiedAction;
    }
    catch
    {
        return null;
    }
}

private ActionInfo AddOperationsToTable(ActionInfo action)
 {
     int clientVersion = action.Version;
     string tableName = action.RoomName;
     …………
     …………
     …………
     ………… 
     List<ActionInfo> actions = GetOperationsQueue(table);
     foreach (ActionInfo info in actions)
     {
      if (!info.IsTransformed)
      {
        CollaborativeEditingHandler.TransformOperation(info, actions);
      }
     }
     action = actions[actions.Count - 1];
     action.Version = updateVersion;
     //Return the transformed operation to broadcast it to other clients.
     return action;
 }

```

# Getting Started with Create React App

This project was bootstrapped with [Create React App](https://github.com/facebook/create-react-app).

## Available Scripts

In the project directory, you can run:

### `npm start`

Runs the app in the development mode.\
Open [http://localhost:3000](http://localhost:3000) to view it in the browser.

The page will reload if you make edits.\
You will also see any lint errors in the console.

### `npm test`

Launches the test runner in the interactive watch mode.\
See the section about [running tests](https://facebook.github.io/create-react-app/docs/running-tests) for more information.

### `npm run build`

Builds the app for production to the `build` folder.\
It correctly bundles React in production mode and optimizes the build for the best performance.

The build is minified and the filenames include the hashes.\
Your app is ready to be deployed!

See the section about [deployment](https://facebook.github.io/create-react-app/docs/deployment) for more information.

### `npm run eject`

**Note: this is a one-way operation. Once you `eject`, you can’t go back!**

If you aren’t satisfied with the build tool and configuration choices, you can `eject` at any time. This command will remove the single build dependency from your project.

Instead, it will copy all the configuration files and the transitive dependencies (webpack, Babel, ESLint, etc) right into your project so you have full control over them. All of the commands except `eject` will still work, but they will point to the copied scripts so you can tweak them. At this point you’re on your own.

You don’t have to ever use `eject`. The curated feature set is suitable for small and middle deployments, and you shouldn’t feel obligated to use this feature. However we understand that this tool wouldn’t be useful if you couldn’t customize it when you are ready for it.

## Learn More

You can learn more in the [Create React App documentation](https://facebook.github.io/create-react-app/docs/getting-started).

To learn React, check out the [React documentation](https://reactjs.org/).
