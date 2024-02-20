import * as React from 'react';
import { DocumentEditorContainerComponent, Toolbar, CollaborativeEditingHandler, ContainerContentChangeEventArgs, Operation, Inject, ToolbarItem } from '@syncfusion/ej2-react-documenteditor';
import { DocumentEditor } from '@syncfusion/ej2-react-documenteditor';
import { TitleBar } from './title-bar';
import { HubConnectionBuilder, HttpTransportType, HubConnectionState, HubConnection } from '@microsoft/signalr';
import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-popups';

DocumentEditor.Inject(CollaborativeEditingHandler);
// tslint:disable:max-line-length
class Editor extends React.Component {
    public serviceUrl = 'https://webapplication120230413155843.azurewebsites.net/';
    public container!: DocumentEditorContainerComponent | null;
    public titleBar?: TitleBar;
    public collaborativeEditingHandler?: CollaborativeEditingHandler;
    public connectionId: string = '';
    public connection?: HubConnection;
    public currentUser: string = 'Guest user';
    public toolbarItems : ToolbarItem[] = ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields']
     public users = ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"];

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
            if (!this.connection) {
                const random = Math.floor(Math.random() * this.users.length);
                this.currentUser = this.users[random];
                this.container.documentEditor.documentName = 'Gaint Panda';
                this.initializeSignalR();
                this.loadDocumentFromServer();
            }
            this.titleBar.updateDocumentTitle();
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
    }

    public onDataRecived(action: string, data: any) {
        if (this.collaborativeEditingHandler) {
            debugger;
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

            this.collaborativeEditingHandler = this.container.documentEditor.collaborativeEditingHandlerModule;
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
        createSpinner({target: document.body});
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
            <div id="toast_type"></div>
            <div style={{ "height": "93vh" }}>
                <div id='documenteditor_titlebar' className="e-de-ctn-title"></div>
                <DocumentEditorContainerComponent id="container" ref={(scope: DocumentEditorContainerComponent) => { this.container = scope; }} style={{ 'display': 'block' }}
                    height={'100%'} currentUser={this.currentUser} toolbarItems={this.toolbarItems} serviceUrl={this.serviceUrl + 'api/documenteditor'} enableToolbar={true} locale='en-US' >
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
