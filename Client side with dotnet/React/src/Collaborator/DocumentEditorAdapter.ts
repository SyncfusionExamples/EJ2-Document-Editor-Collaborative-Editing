import { DocumentEditorContainer, Operation } from "@syncfusion/ej2-react-documenteditor";
import { ICollaborationProvider, ICollaborationActionData } from "@syncfusion/ej2-collaborator";
export class DocumentEditorAdapter implements ICollaborationProvider {
    constructor(
        private container: DocumentEditorContainer,
        private serviceUrl: string,
    ) { }

    // The only ICollaborationProvider method — applied for every remote action. 
    public applyRemoteAction(action: string, data: ICollaborationActionData): void {

        this.container.documentEditor.collaborativeEditingHandlerModule?.applyRemoteAction(action, data.payload);
    }

    // Fetch the document from the product's REST API and return the room name. 
    public async loadFromServer(fileName: string): Promise<string> {
        const roomName: string = this.getRoomName(fileName);
        const response: Response = await fetch(
            this.serviceUrl + 'api/CollaborativeEditing/ImportFile',
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ fileName, roomName })
            }
        );
        if (!response.ok) {
            throw new Error('Failed to load document');
        }
        const responseText: string = await response.text();
        await this.open(responseText, roomName);
        return roomName;
    }


    // Seed the editor and bridge local edits to the editor's sender. 
    public async open(responseText: string, roomName: string): Promise<void> {
        const data: any = JSON.parse(responseText);
        this.container?.documentEditor.collaborativeEditingHandlerModule?.updateRoomInfo(roomName, data.version, this.serviceUrl + 'api/CollaborativeEditing/');
        this.container.documentEditor.open(data.sfdt);
        this.container.contentChange = (args: any) => {
            console.log('[SENT]', new Date().toISOString());
            this.container.documentEditor.collaborativeEditingHandlerModule?.sendActionToServer(args.operations as Operation[]);
        }

    }


    private getRoomName(fileName: string): string {
        const queryString: string = window.location.search;
        const urlParams: URLSearchParams = new URLSearchParams(queryString);
        let roomId: string | null = urlParams.get('id');

        if (!roomId) {
            roomId = Math.random().toString(32).slice(2);
            window.history.replaceState({}, '', '?id=' + roomId);
        }

        return roomId;
    }
}
