import * as React from 'react';
import { useRef } from 'react';
import { DocumentEditorContainerComponent, Toolbar, CollaborativeEditingHandler, ContainerContentChangeEventArgs, Operation, Inject, ToolbarItem } from '@syncfusion/ej2-react-documenteditor';
import { DocumentEditor } from '@syncfusion/ej2-react-documenteditor';
import { TitleBar } from './title-bar';
import { HubConnectionBuilder, HttpTransportType, HubConnectionState, HubConnection } from '@microsoft/signalr';
import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-popups';

// tslint:disable:max-line-length
class Editor extends React.Component {
    public serviceUrl = 'http://localhost:5212/';
    public container!: DocumentEditorContainerComponent | null;
    public titleBar?: TitleBar;
    public collaborativeEditingHandler?: CollaborativeEditingHandler;
    public connectionId: string = '';
    public connection?: HubConnection;
    public currentUser: string = 'Guest user';
    public currentRoomName: string = ''

    public onCreated(): void {
        this.collaborativeEditingHandler = this.container.documentEditor.collaborativeEditingHandlerModule;
        this.container.contentChange = (args: ContainerContentChangeEventArgs) => {
            if (this.collaborativeEditingHandler) {
                //Send the editing action to server
                this.collaborativeEditingHandler.sendActionToServer(args.operations as Operation[])
            }
        }
        if (!this.connection) {
            this.initializeSignalR();
            this.loadDocumentFromServer();
        }
        this.titleBar.updateDocumentTitle();
    }

    public componentDidMount(): void {
        window.onbeforeunload = function () {
            return 'Want to save your changes?';
        }
        if (this.container) {
            this.container.documentEditor.enableCollaborativeEditing = true;
            this.container.documentEditor.pageOutline = '#E0E0E0';
            this.container.documentEditor.acceptTab = true;
            this.container.documentEditor.resize();
            this.titleBar = new TitleBar(document.getElementById('documenteditor_titlebar') as HTMLElement, this.container.documentEditor, true);
            //Inject the collaborative editing handler to DocumentEditor
            DocumentEditor.Inject(CollaborativeEditingHandler);
            //Enable the collaborative editing in DocumentEditor
            this.container.documentEditor.enableCollaborativeEditing = true;
        }
    }

    public initializeSignalR = (): void => {
        // SignalR connection
        this.connection = new HubConnectionBuilder().withUrl(this.serviceUrl + 'documenteditorhub', {
            skipNegotiation: true,
            transport: HttpTransportType.WebSockets
        }).withAutomaticReconnect().build();
        //Event handler for signalR connection
        this.connection.on('dataReceived', this.onDataRecived.bind(this));

        this.connection.onclose(async () => {
            if (this.connection && this.connection.state === HubConnectionState.Disconnected) {
                alert('Connection lost. Please relod the browser to continue.');
            }
        });
        this.connection.onreconnected(() => {
            if (this.connection && this.currentRoomName != null) {
                this.connection.send('JoinGroup', { roomName: this.currentRoomName, currentUser: this.currentUser });
            }
            console.log('server reconnected!!!');
        });
    }

    public onDataRecived(action: string, data: any) {
        if (this.collaborativeEditingHandler) {
            if (action == 'connectionId') {
                //Update the current connection id to track other users
                this.connectionId = data;
            } else if (this.connectionId != data.connectionId) {
                if (this.titleBar) {
                    if (action == 'action' || action == 'addUser') {
                        //Add the user to title bar when user joins the room
                        this.titleBar.addUser(data);
                    } else if (action == 'removeUser') {
                        //Remove the user from title bar when user leaves the room
                        this.titleBar.removeUser(data);
                    }
                }
            }
            //Apply the remote action in DocumentEditor
            this.collaborativeEditingHandler.applyRemoteAction(action, data);
        }
    }

    public openDocument(responseText: string, roomName: string): void {

        let data = JSON.parse(responseText);
        if (this.container) {
            //Update the room and version information to collaborative editing handler.
            this.collaborativeEditingHandler.updateRoomInfo(roomName, data.version, this.serviceUrl + 'api/CollaborativeEditing/');

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
        let roomId = urlParams.get('id');
        if (roomId == null) {
            roomId = Math.random().toString(32).slice(2)
            window.history.replaceState({}, "", `?id=` + roomId);
        }
        var httpRequest = new XMLHttpRequest();
        httpRequest.open('Post', this.serviceUrl + 'api/CollaborativeEditing/ImportFile', true);
        httpRequest.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
        httpRequest.onreadystatechange = () => {
            if (httpRequest.readyState === 4) {
                if (httpRequest.status === 200 || httpRequest.status === 304) {
                    this.openDocument(httpRequest.responseText, roomId as string);
                    hideSpinner(document.body);
                }
                else {
                    hideSpinner(document.body);
                    alert('Fail to load the document');
                }
            }
        };
        httpRequest.send(JSON.stringify({ "fileName": "Giant Panda.docx", "roomName": roomId }));
    }

    public connectToRoom(data: any) {
        try {
            this.currentRoomName = data.roomName;
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
            //Attempting to reconnect in 5 seconds
            setTimeout(this.connectToRoom, 5000);
        }
    };

    render() {
        return (<div className='control-pane'>
            <div>
                <div id='documenteditor_titlebar' className="e-de-ctn-title"></div>
                <div id="documenteditor_container_body">
                    <DocumentEditorContainerComponent id="container" created={this.onCreated.bind(this)} ref={(scope) => { this.container = scope; }} style={{ 'display': 'block' }}
                        height={'590px'} currentUser={this.currentUser} serviceUrl={this.serviceUrl + 'api/documenteditor'} enableToolbar={true} locale='en-US' >
                        <Inject services={[Toolbar]} />
                    </DocumentEditorContainerComponent>
                </div>
            </div>
        </div>);
    }

}
export default Editor;

