import { Component, ViewChild } from '@angular/core';
import { DocumentEditorContainerModule, ToolbarService, DocumentEditorContainerComponent, ContainerContentChangeEventArgs, Operation } from '@syncfusion/ej2-angular-documenteditor';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { DocumentEditor, CollaborativeEditingHandler } from '@syncfusion/ej2-documenteditor';
import { TitleBar } from "./title-bar"
import { HubConnectionBuilder, HttpTransportType, HubConnectionState, HubConnection } from '@microsoft/signalr';
import { hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import { isNullOrUndefined } from '@syncfusion/ej2-base';

DocumentEditor.Inject(CollaborativeEditingHandler);
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [DocumentEditorContainerModule, CommonModule, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  providers: [ToolbarService],
})
export class AppComponent {
  title = 'syncfusion-angular-app';
  
  @ViewChild("documenteditor_default")
  private container!: DocumentEditorContainerComponent;

  private collaborativeEditingHandler!: CollaborativeEditingHandler;
  private serviceUrl: string = "http://localhost:5212/";
  public connection?: HubConnection;
  public titleBar?: TitleBar;
  public connectionId: string = '';
  public currentUser: string = 'Guest user';


  onCreated() {
    this.container.documentEditor.documentName = 'Getting Started';
    //Enable collaborative editing in Document Editor.
    this.container.documentEditor.enableCollaborativeEditing = true;
    
    //Title bar implementation
    this.titleBar = new TitleBar(document.getElementById('documenteditor_titlebar') as HTMLElement, this.container.documentEditor, true);
    this.titleBar.updateDocumentTitle();
    this.initializeSignalR();
    this.loadDocumentFromServer();
  }

  onContentChange = (args: ContainerContentChangeEventArgs) => {
    if(isNullOrUndefined(this.collaborativeEditingHandler)) {
      this.collaborativeEditingHandler = this.container.documentEditor.collaborativeEditingHandlerModule;
    }
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

  openDocument(responseText: string, roomName: string): void {
    showSpinner(document.getElementById('container') as HTMLElement);

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
    hideSpinner(document.getElementById('container') as HTMLElement);
  }

  loadDocumentFromServer() {
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
          hideSpinner(document.getElementById('container') as HTMLElement);
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
