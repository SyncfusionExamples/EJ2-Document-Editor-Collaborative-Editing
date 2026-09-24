import {
    DocumentEditor,
    DocumentEditorContainer,
    Operation,ContentChangeEventArgs
} from "@syncfusion/ej2-documenteditor";

import { ICollaborationProvider, ICollaborationActionData } from "@syncfusion/ej2-collaborator";

export class DocumentEditorAdapter implements ICollaborationProvider {

    constructor(
        private container: DocumentEditorContainer,    
        private serviceUrl: string,    
    ) { }

  public async loadFromServer(fileName: string): Promise<string> {
        const roomName: string = this.getRoomName(fileName);
        const response: Response = await fetch(
            this.serviceUrl +'api/CollaborativeEditing/ImportFile',
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

     public async open(responseText: string, roomName: string): Promise<void> {
        const data: any = JSON.parse(responseText);
        this.container?.documentEditor.collaborativeEditingHandlerModule?.updateRoomInfo(roomName, data.version, this.serviceUrl + 'api/CollaborativeEditing/');
        this.container.documentEditor.open(data.sfdt);       
        this.container.documentEditor.contentChange = (args: ContentChangeEventArgs) => {     
            console.log('[SENT]',new Date().toISOString());     
           this.container.documentEditor.collaborativeEditingHandlerModule?.sendActionToServer(args.operations as Operation[]);
        }               

    }

    public applyRemoteAction( action: string,data:ICollaborationActionData): void {       
      
          this.container.documentEditor.collaborativeEditingHandlerModule?.applyRemoteAction( action,data.payload);
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
