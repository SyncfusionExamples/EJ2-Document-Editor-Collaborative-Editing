export class DocumentEditorAdapter {

    constructor(container, serviceUrl) {
        this.container = container;
        this.serviceUrl = serviceUrl;
    }

    // The only ICollaborationProvider method — applied for every remote action.
    applyRemoteAction(action, data) {
        this.container.documentEditor.collaborativeEditingHandlerModule?.applyRemoteAction(action, data.payload);
    }

    async loadFromServer(fileName) {
        const roomName = this.getRoomName();
        const response = await fetch(
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
        const responseText = await response.text();
        await this.open(responseText, roomName);
        return roomName;
    }

    async open(responseText, roomName) {
        const data = JSON.parse(responseText);
        this.container?.documentEditor.collaborativeEditingHandlerModule?.updateRoomInfo(roomName, data.version, this.serviceUrl + 'api/CollaborativeEditing/');
        this.container.documentEditor.open(data.sfdt);
        this.container.contentChange = (args) => {
            console.log('[SENT]', new Date().toISOString());
            this.container.documentEditor.collaborativeEditingHandlerModule?.sendActionToServer(args.operations);
        }
    }

    getRoomName() {
        const queryString = window.location.search;
        const urlParams = new URLSearchParams(queryString);
        let roomId = urlParams.get('id');

        if (!roomId) {
            roomId = Math.random().toString(32).slice(2);
            window.history.replaceState({}, '', '?id=' + roomId);
        }

        return roomId;
    }
}
