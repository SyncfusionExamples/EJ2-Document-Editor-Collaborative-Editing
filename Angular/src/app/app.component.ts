import { Component, ViewChild } from '@angular/core';
import { DocumentEditorContainerModule, ToolbarService, DocumentEditorContainerComponent, ContainerContentChangeEventArgs, Operation } from '@syncfusion/ej2-angular-documenteditor';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { DocumentEditor, CollaborativeEditingHandler } from '@syncfusion/ej2-documenteditor';
import { TitleBar } from "./title-bar"
import { HubConnectionBuilder, HttpTransportType, HubConnectionState, HubConnection } from '@microsoft/signalr';
import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-popups';

DocumentEditor.Inject(CollaborativeEditingHandler);
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [DocumentEditorContainerModule, CommonModule, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  providers: [ToolbarService],
})
export class AppComponent {
  title = 'syncfusion-angular-app';
  @ViewChild("documenteditor_default")
  private container!: DocumentEditorContainerComponent;

  private collaborativeEditingHandler!: CollaborativeEditingHandler;
  private serviceUrl: string = "https://webapplication120230413155843.azurewebsites.net/";
  public connection?: HubConnection;
  public titleBar?: TitleBar;
  public connectionId: string = '';
  public currentUser: string = 'Guest user';
  public toolbarItems = ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields']
  public users = ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"];


  onCreated() {
    const random = Math.floor(Math.random() * this.users.length);
    this.currentUser = this.users[random];
    this.container.documentEditor.documentName = 'Gaint Panda';
    //Enable collaborative editing in Document Editor.
    this.container.documentEditor.enableCollaborativeEditing = true;
    this.collaborativeEditingHandler = this.container.documentEditor.collaborativeEditingHandlerModule;
    //Title bar implementation
    this.titleBar = new TitleBar(document.getElementById('documenteditor_titlebar') as HTMLElement, this.container.documentEditor, true);
    this.titleBar.updateDocumentTitle();
    this.initializeSignalR();
    this.loadDocumentFromServer();
  }

  onContentChange = (args: ContainerContentChangeEventArgs) => {
    if (this.collaborativeEditingHandler) {
      //Send the editing action to server
      this.collaborativeEditingHandler.sendActionToServer(args.operations as Operation[])
    }
  }

  initializeSignalR = (): void => {
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

  onDataRecived(action: string, data: any) {
    if (this.collaborativeEditingHandler) {
      debugger;
      if (action == 'connectionId') {
        //Update the current connection id to track other users
        this.connectionId = data;
      } else if (this.connectionId != data.connectionId) {
        if (this.titleBar) {
          if (action == 'action' || action == 'addUser') {
            //Add the user to title bar when user joins the room
            this.titleBar.updateUserInfo(data, 'addUser');
          } else if (action == 'removeUser') {
            //Remove the user from title bar when user leaves the room
            this.titleBar.updateUserInfo(data, 'removeUser');
          }
        }
      }
      //Apply the remote action in DocumentEditor
      this.collaborativeEditingHandler.applyRemoteAction(action, data);
    }
  }

  openDocument(responseText: string, roomName: string): void {
    

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

  loadDocumentFromServer() {
    createSpinner({ target: document.body });
    showSpinner(document.body);
    const queryString = window.location.search;
    const urlParams = new URLSearchParams(queryString);
    let roomId = urlParams.get('roomId');
    if (roomId == null) {
      roomId = Math.random().toString(32).slice(2)
      window.history.replaceState({}, "", `?roomId=` + roomId);
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

}
