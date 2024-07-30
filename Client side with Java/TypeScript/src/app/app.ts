import { ContainerContentChangeEventArgs, DocumentEditorContainer, CollaborativeEditingHandler, DocumentEditor, Toolbar, Operation, ToolbarItem } from '@syncfusion/ej2-documenteditor';
import { TitleBar } from './title-bar';
import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import { Stomp, CompatClient } from '@stomp/stompjs';
import SockJS from 'sockjs-client';

//Collaborative editing controller url
let serviceUrl = 'http://localhost:8024/';
let connectionId: string = "";
let toolbarItems: ToolbarItem[] = ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields']
let users = ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"];
let currentRoomName: string = '';
let webSocketEndPoint: string = 'http://localhost:8024/ws';
let stompClient!: CompatClient;

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
    if (container.documentEditor.collaborativeEditingHandlerModule) {
        //Send the editing action to server
        container.documentEditor.collaborativeEditingHandlerModule.sendActionToServer(args.operations as Operation[])
    }
}


function initializeSockJs(): void {
    let ws = new SockJS(webSocketEndPoint);
    stompClient = Stomp.over(ws);


    stompClient.connect({}, () => {
        onConnected();
    }, (error: any) => {
        console.error('Error during WebSocket connection', error);
    });
}

function onConnected() {
    if (stompClient.connected) {
        // Subscribe to the specific topic            
        stompClient.subscribe('/topic/public/' + currentRoomName, onDataRecived);
        connectToRoom(currentRoomName);
        console.log('server connected!!!');
    } else {
        console.log('Waiting for WebSocket connection...');
    }
}

//Method to process the data received from server
function onDataRecived(data: any) {
    if (container.documentEditor.collaborativeEditingHandlerModule) {
        var content = JSON.parse(data.body);
        var action = content.headers['action'];
        debugger;

        if (content.payload.operations != null) {
            container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("action", content.payload);
        }
        else if (action == "removeUser") {
            container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("removeUser", content.payload.connectionId);
            titleBar.removeUser(content.payload.connectionId);
        } else if (action == "connectionId" && connectionId == "") {
            connectionId = content.payload.connectionId;
            container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("connectionId", content.payload.connectionId);
        }
        else if (Array.isArray(content.payload)) {
            titleBar.addUser(content.payload, connectionId);
        }
    }
}



function connectToRoom(data: any) {
    var userInfo = {
        currentUser: container.currentUser,
        clientVersion: 0,
        roomName: currentRoomName,
        connectionId: "",
    };
    //Send the user information to server
    stompClient.send("/app/join/" + currentRoomName, {}, JSON.stringify(userInfo));
};


function openDocument(responseText: string, roomName: string): void {
    let data = JSON.parse(responseText);   
    //Update the room and version information to collaborative editing handler.
    container.documentEditor.collaborativeEditingHandlerModule.updateRoomInfo(roomName, data.version, serviceUrl + 'api/collaborativeediting/');
    //Open the document
    container.documentEditor.open(data.sfdt);
    setTimeout(function () {
        // connect to server using signalR
        connectToRoom({ action: 'connect', roomName: roomName, currentUser: container.currentUser });
    });
    hideSpinner(document.body as HTMLElement);
}

function loadDocumentFromServer() {
    createSpinner({ target: document.body });
    showSpinner(document.body as HTMLElement);
    const queryString = window.location.search;
    const urlParams = new URLSearchParams(queryString);
    //Get the room id from the query string
    let roomId = urlParams.get('id');
    if (roomId == null) {
        // If room id is not available, generate a new room id
        // This random is used for demo purpose only. In real-time scenario, you need to generate a unique room id for each document and maintain it in the server and 
        // reuse it when the user reopens the document.
        roomId = Math.random().toString(32).slice(2)
        window.history.replaceState({}, "", `?id=` + roomId);
    }
    currentRoomName = roomId;
    var httpRequest = new XMLHttpRequest();
    httpRequest.open('Post', serviceUrl + 'api/collaborativeediting/ImportFile', true);
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

initializeSockJs();
loadDocumentFromServer();
