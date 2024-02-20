import { ContainerContentChangeEventArgs, DocumentEditorContainer, CollaborativeEditingHandler, DocumentEditor, Toolbar, Operation, ToolbarItem } from '@syncfusion/ej2-documenteditor';
import { TitleBar } from './title-bar';
import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import { HubConnectionBuilder, HttpTransportType, HubConnectionState } from '@microsoft/signalr';

//Collaborative editing controller url
let serviceUrl = 'https://webapplication120230413155843.azurewebsites.net/';
let collborativeEditingHandler: CollaborativeEditingHandler;
let connectionId: string = "";
let toolbarItems: ToolbarItem[] = ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields']
let users = ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"];


/**
 * Container component
 */
let container: DocumentEditorContainer = new DocumentEditorContainer({ height: "100%", toolbarItems: toolbarItems, enableToolbar: true, currentUser: 'Guest User' });
container.serviceUrl = serviceUrl + 'api/documenteditor/';
DocumentEditorContainer.Inject(Toolbar);
container.appendTo('#container');


const random = Math.floor(Math.random() * users.length);
container.currentUser = users[random];
container.documentEditor.documentName = 'Gaint Panda';

//Injecting collaborative editing module
DocumentEditor.Inject(CollaborativeEditingHandler);
//Enable collaborative editing in DocumentEditor
container.documentEditor.enableCollaborativeEditing = true;

//Title bar implementation
let titleBar: TitleBar = new TitleBar(document.getElementById('documenteditor_titlebar') as HTMLElement, container.documentEditor, true);
titleBar.updateDocumentTitle();

container.contentChange = function (args: ContainerContentChangeEventArgs) {
    if (collborativeEditingHandler) {
        //Send the editing action to server
        collborativeEditingHandler.sendActionToServer(args.operations as Operation[])
    }
}

// SignalR connection
var connection = new HubConnectionBuilder().withUrl(serviceUrl + 'documenteditorhub', {
    skipNegotiation: true,
    transport: HttpTransportType.WebSockets
}).withAutomaticReconnect().build();


async function connectToRoom(data: any) {
    try {
        // start the connection.
        connection.start().then(function () {
            // Join the room.
            connection.send('JoinGroup', { roomName: data.roomName, currentUser: data.currentUser });
            console.log('server connected!!!');
        });
    } catch (err) {
        console.log(err);
        //Attempting to reconnect in 5 seconds
        setTimeout(connectToRoom, 5000);
    }
};

//Event handler for signalR connection
connection.on('dataReceived', onDataRecived.bind(this));

//Method to process the data received from server
function onDataRecived(action: string, data: any) {
    if (collborativeEditingHandler) {
        if (action == 'connectionId') {
            //Update the current connection id to track other users
            connectionId = data;
        } else if (connectionId != data.connectionId) {
            if (action == 'action' || action == 'addUser') {
                //Add the user to title bar when user joins the room
                titleBar.updateUserInfo(data, 'addUser');
            } else if (action == 'removeUser') {
                //Remove the user from title bar when user leaves the room
                titleBar.updateUserInfo(data, 'removeUser');
            }
        }
        //Apply the remote action in DocumentEditor
        collborativeEditingHandler.applyRemoteAction(action, data);
    }
}

connection.onclose(async () => {
    if (connection.state === HubConnectionState.Disconnected) {
        alert('Connection lost. Please relod the browser to continue.');
    }
});


function openDocument(responseText: string, roomName: string): void {
    

    let data = JSON.parse(responseText);

    collborativeEditingHandler = container.documentEditor.collaborativeEditingHandlerModule;
    //Update the room and version information to collaborative editing handler.
    collborativeEditingHandler.updateRoomInfo(roomName, data.version, serviceUrl + 'api/CollaborativeEditing/');

    //Open the document
    container.documentEditor.open(data.sfdt);

    setTimeout(function () {
        // connect to server using signalR
        connectToRoom({ action: 'connect', roomName: roomName, currentUser: container.currentUser });
    });

    hideSpinner(document.body as HTMLElement);
}

function loadDocumentFromServer() {
    createSpinner({target:document.body});
    showSpinner(document.body as HTMLElement);
    const queryString = window.location.search;
    const urlParams = new URLSearchParams(queryString);
    let roomId = urlParams.get('id');

    if (roomId == null) {
        roomId = Math.random().toString(32).slice(2)
        window.history.replaceState({}, "", `?id=` + roomId);
    }
    var httpRequest = new XMLHttpRequest();
    httpRequest.open('Post', serviceUrl + 'api/CollaborativeEditing/ImportFile', true);
    httpRequest.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    httpRequest.onreadystatechange = function () {
        if (httpRequest.readyState === 4) {
            if (httpRequest.status === 200 || httpRequest.status === 304) {


                openDocument(httpRequest.responseText, roomId as string);
            }
            else {
                hideSpinner(document.body as HTMLElement);
                alert('Fail to load the document');
            }
        }
    };
    httpRequest.send(JSON.stringify({ "fileName": "Giant Panda.docx", "roomName": roomId }));
}

loadDocumentFromServer();
