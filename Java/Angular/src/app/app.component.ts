import { Component, ViewChild } from '@angular/core';
import { DocumentEditorContainerModule, ToolbarService, DocumentEditorContainerComponent, ContainerContentChangeEventArgs, Operation } from '@syncfusion/ej2-angular-documenteditor';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { DocumentEditor, CollaborativeEditingHandler } from '@syncfusion/ej2-documenteditor';
import { TitleBar } from "./title-bar"
import { Stomp, CompatClient } from '@stomp/stompjs';
import * as SockJS from 'sockjs-client';
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
  public serviceUrl = 'http://localhost:8024/';
  public titleBar?: TitleBar;
  public connectionId: string = '';
  public currentUser: string = 'Guest user';
  public toolbarItems = ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields']
  public users = ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"];
  public currentRoomName: string = '';
  public webSocketEndPoint: string = 'http://localhost:8024/ws';
  public stompClient!: CompatClient;



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
    this.initializeSockJs();
    this.loadDocumentFromServer();
  }

  onContentChange = (args: ContainerContentChangeEventArgs) => {
    if (this.collaborativeEditingHandler) {
      //Send the editing action to server
      this.collaborativeEditingHandler.sendActionToServer(args.operations as Operation[])
    }
  }

  initializeSockJs = (): void => {
    let ws = new SockJS(this.webSocketEndPoint);
    this.stompClient = Stomp.over(ws);
    const _this = this;

    _this.stompClient.connect({}, () => {
      _this.onConnected();
    }, (error: any) => {
      console.error('Error during WebSocket connection', error);
    });
  }
  onConnected() {
    if (this.stompClient.connected) {
      // Subscribe to the specific topic            
      this.stompClient.subscribe('/topic/public/' + this.currentRoomName, this.onDataRecived.bind(this));
      this.connectToRoom(this.currentRoomName);
      console.log('server connected!!!');
    } else {
      console.log('Waiting for WebSocket connection...');
    }
  }

  onDataRecived(data: any) {
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
        this.titleBar?.updateUserInfo(content.payload.connectionId, 'removeUser');
      } else if (action == "connectionId" && this.connectionId == "") {
        this.connectionId = content.payload.connectionId;
        this.container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("connectionId", content.payload.connectionId);
      }
      else if (Array.isArray(content.payload)) {
        this.titleBar?.updateUserInfo(content.payload, 'addUser');
      }
    }
  }

  openDocument(responseText: string, roomName: string): void {
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

  loadDocumentFromServer() {
    createSpinner({ target: document.body });
    showSpinner(document.body);
    const queryString = window.location.search;
    const urlParams = new URLSearchParams(queryString);
    let roomId = urlParams.get('roomId');
    //Get the room id from the query string
    if (roomId == null) {
      // If room id is not available, generate a new room id
      // This random is used for demo purpose only. In real-time scenario, you need to generate a unique room id for each document and maintain it in the server and 
      // reuse it when the user reopens the document.
      roomId = Math.random().toString(32).slice(2)
      window.history.replaceState({}, "", `?roomId=` + roomId);
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

}
