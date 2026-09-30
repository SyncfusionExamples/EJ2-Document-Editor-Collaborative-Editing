import * as React from 'react';
import { DocumentEditorContainerComponent, Ribbon, CollaborativeEditingHandler } from '@syncfusion/ej2-react-documenteditor';
import { DocumentEditor } from '@syncfusion/ej2-react-documenteditor';
import { TitleBar } from './title-bar';
import { CollaborationClient, UserInfo, } from "@syncfusion/ej2-collaborator";
import { DocumentEditorAdapter } from "./Collaborator/DocumentEditorAdapter";
// tslint:disable:max-line-length
class Editor extends React.Component {
    public serviceUrl = 'http://localhost:5212/';
    public container!: DocumentEditorContainerComponent | null;
    public titleBar?: TitleBar;
    public collaborativeEditingHandler?: CollaborativeEditingHandler;
    public currentUser: string = 'Guest user';
    public onCreated(): void {
        this.collaborativeEditingHandler = this.container!.documentEditor.collaborativeEditingHandlerModule;
        this.titleBar!.updateDocumentTitle();
    }

    public componentDidMount(): void {
        window.onbeforeunload = function () {
            return 'Want to save your changes?';
        }
        if (this.container) {
            this.container.documentEditor.pageOutline = '#E0E0E0';
            this.container.documentEditor.acceptTab = true;
            this.container.documentEditor.resize();
            this.titleBar = new TitleBar(document.getElementById('documenteditor_titlebar') as HTMLElement, this.container.documentEditor, true);
            //Inject the collaborative editing handler to DocumentEditor
           // DocumentEditor.Inject(CollaborativeEditingHandler);
            DocumentEditorContainerComponent.Inject(Ribbon, CollaborativeEditingHandler);
            DocumentEditor.Inject(CollaborativeEditingHandler);
            //Enable the collaborative editing in DocumentEditor
            this.container.documentEditor.enableCollaborativeEditing = true;
        }
        const adapter = new DocumentEditorAdapter(this.container!, 'http://localhost:5212/');
        const client = new CollaborationClient(adapter, {
            serviceUrl: 'http://localhost:5212/',
            currentUser: this.currentUser,
            connectionType: "signalr",

            onUserJoined: (user: any) => {
                console.log("User Joined", user);
                this.titleBar!.addUser(user);
            },
            onUserLeft: (user: any) => {
                console.log("User Left", user);
                this.titleBar!.removeUser(user);
            }
        });
        (async () => {
            const roomName = await adapter.loadFromServer("Giant Panda.docx");
            await client.joinRoomAsync(roomName);
        })();

    };
    render() {
        return (<div className='control-pane'>
            <div>
                <div id='documenteditor_titlebar' className="e-de-ctn-title"></div>
                <div id="documenteditor_container_body">
                    <DocumentEditorContainerComponent id="container" created={this.onCreated.bind(this)} ref={(scope: DocumentEditorContainerComponent) => { this.container = scope; }} style={{ 'display': 'block' }}
                        height={'590px'} currentUser={this.currentUser} serviceUrl={this.serviceUrl + 'api/documenteditor'} enableToolbar={true} locale='en-US'
                        toolbarMode={"Ribbon"}    >
                    </DocumentEditorContainerComponent>
                </div>
            </div>
        </div>);
    }
}
export default Editor;

