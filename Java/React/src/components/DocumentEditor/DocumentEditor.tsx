import * as React from 'react';
import { DocumentEditorContainerComponent, Toolbar, CollaborativeEditingHandler, ContainerContentChangeEventArgs, Operation, Inject, ToolbarItem } from '@syncfusion/ej2-react-documenteditor';
import { DocumentEditor } from '@syncfusion/ej2-react-documenteditor';
import { TitleBar } from './title-bar';
import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import * as SockJS from 'sockjs-client';
import { Stomp, CompatClient } from '@stomp/stompjs';

DocumentEditor.Inject(CollaborativeEditingHandler);
// tslint:disable:max-line-length
class Editor extends React.Component {
    public serviceUrl = 'http://localhost:8024/';
    public container!: DocumentEditorContainerComponent | null;
    public titleBar?: TitleBar;
    public collaborativeEditingHandler?: CollaborativeEditingHandler;
    public connectionId: string = '';
    public currentUser: string = 'Guest user';
    public toolbarItems: ToolbarItem[] = ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields']
    public users = ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"];
    public currentRoomName: string = ''
    webSocketEndPoint: string = 'http://localhost:8024/ws';
    public stompClient!: CompatClient;

    public componentDidMount(): void {
        window.onbeforeunload = function () {
            return 'Want to save your changes?';
        }
        if (this.container) {
            this.container.documentEditor.enableCollaborativeEditing = true;
            this.container.documentEditor.resize();
            this.titleBar = new TitleBar(document.getElementById('documenteditor_titlebar') as HTMLElement, this.container.documentEditor, true);

            this.collaborativeEditingHandler = this.container.documentEditor.collaborativeEditingHandlerModule;

            this.container.contentChange = (args: ContainerContentChangeEventArgs) => {
                if (this.collaborativeEditingHandler) {
                    //Send the editing action to server
                    this.collaborativeEditingHandler.sendActionToServer(args.operations as Operation[])
                }
            }
            if (!this.stompClient) {
                const random = Math.floor(Math.random() * this.users.length);
                this.currentUser = this.users[random];
                this.container.documentEditor.documentName = 'Gaint Panda';
                this.initializeSockJs();
                this.loadDocumentFromServer();
            }
            this.titleBar.updateDocumentTitle();
        }
    }

    public initializeSockJs = (): void => {
        let ws = new SockJS(this.webSocketEndPoint);
        this.stompClient = Stomp.over(ws);
        const _this = this;

        _this.stompClient.connect({}, () => {
            _this.onConnected();
        }, (error: any) => {
            console.error('Error during WebSocket connection', error);
        });
    }

    public onConnected() {
        if (this.stompClient.connected) {
            // Subscribe to the specific topic            
            this.stompClient.subscribe('/topic/public/' + this.currentRoomName, this.onDataRecived.bind(this));
            this.connectToRoom(this.currentRoomName);
            console.log('server connected!!!');
        } else {
            console.log('Waiting for WebSocket connection...');
        }
    }



    public onDataRecived(data: any) {
        if (this.collaborativeEditingHandler) {
            var content = JSON.parse(data.body);
            const _this = this;
            var action = content.headers['action'];
            debugger;

            if (content.payload.operations != null) {
                this.container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("action", content.payload);
            }
            else if (action == "removeUser") {
                this.container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("removeUser", content.payload.connectionId);
                this.titleBar.removeUser(content.payload.connectionId);
            } else if (action == "connectionId" && this.connectionId == "") {
                this.connectionId = content.payload.connectionId;
                this.container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("connectionId", content.payload.connectionId);
            }
            else if (Array.isArray(content.payload)) {
                this.titleBar.addUser(content.payload, this.connectionId);
            }
        }
    }

    public openDocument(responseText: string, roomName: string): void {
        let data = JSON.parse(responseText);
        if (this.container) {

            this.collaborativeEditingHandler = this.container.documentEditor.collaborativeEditingHandlerModule;
            //Update the room and version information to collaborative editing handler.
            this.collaborativeEditingHandler.updateRoomInfo(roomName, data.version, this.serviceUrl + 'api/collaborativeediting/');

            //Open the document
            this.container.documentEditor.open(data.sfdt);

            setTimeout(() => {
                if (this.container) {
                    // connect to server using signalR
                    this.connectToRoom({ action: 'connect', roomName: roomName, currentUser: this.container.currentUser });
                }
            });
        }
        hideSpinner(document.body);
    }

    public loadDocumentFromServer() {
        createSpinner({ target: document.body });
        showSpinner(document.body);
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
        this.currentRoomName = roomId;
        var httpRequest = new XMLHttpRequest();
        httpRequest.open('Post', this.serviceUrl + 'api/collaborativeediting/ImportFile', true);
        httpRequest.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
        httpRequest.onreadystatechange = () => {
            if (httpRequest.readyState === 4) {
                if (httpRequest.status === 200 || httpRequest.status === 304) {
                    this.openDocument(httpRequest.responseText, roomId as string);
                }
                else {
                    hideSpinner(document.body);
                    alert('Fail to load the document');
                }
            }
        };
        //Sent request to server to get the document with name `Giant Panda.docx`
        httpRequest.send(JSON.stringify({ "fileName": "Giant Panda.docx", "roomName": roomId }));
    }

    public connectToRoom(data: any) {
        var userInfo = {
            currentUser: this.currentUser,
            clientVersion: 0,
            roomName: this.currentRoomName,
            connectionId: "",
        };
        //Send the user information to server
        this.stompClient.send("/app/join/" + this.currentRoomName, {}, JSON.stringify(userInfo));
    };

    render() {
        return (<div className='control-pane'>
            <div id="toast_type"></div>
            <div style={{ "height": "93vh" }}>
                <div id='documenteditor_titlebar' className="e-de-ctn-title"></div>
                <DocumentEditorContainerComponent id="container" ref={(scope: DocumentEditorContainerComponent) => { this.container = scope; }} style={{ 'display': 'block' }}
                    height={'100%'} currentUser={this.currentUser} toolbarItems={this.toolbarItems} serviceUrl={this.serviceUrl + 'api/wordeditor'} enableToolbar={true} locale='en-US' >
                    <Inject services={[Toolbar]} />
                </DocumentEditorContainerComponent>
            </div>
            <div id="defaultDialog" style={{ "display": "none" }}>
                <div className="e-de-para-dlg-heading">
                    Share this URL with others for real-time editing
                </div>
                <div className="e-de-container-row">
                    <input type="text" id="share_url" className="e-input" readOnly />
                </div>
            </div>
        </div>);
    }

}
export default Editor;
