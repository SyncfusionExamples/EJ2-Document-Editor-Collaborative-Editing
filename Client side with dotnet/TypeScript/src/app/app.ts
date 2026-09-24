import { ContainerContentChangeEventArgs, DocumentEditorContainer, CollaborativeEditingHandler, DocumentEditor, Toolbar, Operation } from '@syncfusion/ej2-documenteditor';
import { TitleBar } from './title-bar';
import { hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import { CollaborationClient, UserInfo,  } from "@syncfusion/ej2-collaborator";
import { DocumentEditorAdapter } from "../Collaborator/DocumentEditorAdapter";

//Collaborative editing controller url
let serviceUrl = 'http://localhost:5212/';

DocumentEditorContainer.Inject(Toolbar);
//Injecting collaborative editing module
DocumentEditor.Inject(CollaborativeEditingHandler);
/**
 * Container component
 */

let container: DocumentEditorContainer = new DocumentEditorContainer({
    enableToolbar: true,
    height: '590px',
    currentUser: 'Guest User',
    serviceUrl: serviceUrl + 'api/documenteditor'
});
container.appendTo('#container');
//Enable collaborative editing in DocumentEditor
container.documentEditor.enableCollaborativeEditing = true;

//Title bar implementation
let titleBar: TitleBar = new TitleBar(document.getElementById('documenteditor_titlebar') as HTMLElement, container.documentEditor, true);
container.documentEditor.documentName = 'Getting Started';
titleBar.updateDocumentTitle();

const adapter = new DocumentEditorAdapter(container, serviceUrl); //ServiceURL where collaborative editing web action methods are provided
const client = new CollaborationClient(adapter, { 
    serviceUrl:"http://localhost:5212", 
    currentUser: 'Guest User',
    connectionType: 'signalr',

    onUserJoined: (user: UserInfo) => {
        console.log("User Joined", user);
        titleBar.addUser(user);
    },
    onUserLeft: (user: UserInfo) => {
        console.log("User Left", user);
        titleBar.removeUser(user);
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





