import { Component, ViewChild } from '@angular/core';
import { DocumentEditorContainerModule, ToolbarService, DocumentEditorContainerComponent, ContainerContentChangeEventArgs, Operation } from '@syncfusion/ej2-angular-documenteditor';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { DocumentEditor, CollaborativeEditingHandler } from '@syncfusion/ej2-documenteditor';
import { TitleBar } from "./title-bar"
import { HubConnectionBuilder, HttpTransportType, HubConnectionState, HubConnection } from '@microsoft/signalr';
import { hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import { isNullOrUndefined } from '@syncfusion/ej2-base';
import { CollaborationClient, UserInfo } from "@syncfusion/ej2-collaborator";
import { DocumentEditorAdapter } from "./Collaborator/DocumentEditorAdapter";

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
  public currentRoomName: string = '';
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

    const adapter = new DocumentEditorAdapter(this.container, 'http://localhost:5212/');
    const client = new CollaborationClient(adapter, {
      serviceUrl: "http://localhost:5212",
      currentUser: this.currentUser,
      connectionType: "signalr",
      onUserJoined: (user: UserInfo) => {
        console.log("User Joined", user);
        this.titleBar?.addUser(user);
      },
      onUserLeft: (user: UserInfo) => {
        console.log("User Left", user);
        this.titleBar?.removeUser(user);
      }
    });
    (async () => {

      const roomName =
        await adapter.loadFromServer(
          "Giant Panda.docx"
        );

      await client.joinRoomAsync(
        roomName
      );

    })();

  }


}
