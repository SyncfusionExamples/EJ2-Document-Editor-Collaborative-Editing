<style>
@import '../node_modules/@syncfusion/ej2-base/styles/material.css';
@import '../node_modules/@syncfusion/ej2-buttons/styles/material.css';
@import '../node_modules/@syncfusion/ej2-inputs/styles/material.css';
@import '../node_modules/@syncfusion/ej2-popups/styles/material.css';
@import '../node_modules/@syncfusion/ej2-lists/styles/material.css';
@import '../node_modules/@syncfusion/ej2-navigations/styles/material.css';
@import '../node_modules/@syncfusion/ej2-splitbuttons/styles/material.css';
@import '../node_modules/@syncfusion/ej2-dropdowns/styles/material.css';
@import "../node_modules/@syncfusion/ej2-vue-documenteditor/styles/material.css";
</style>

<template>
  <div id="spinner">
    <ejs-documenteditorcontainer ref="doceditcontainer" :contentChange="onContentChange" :serviceUrl='serviceUrl'
      :enableToolbar='true' v-bind:created="onCreated">
    </ejs-documenteditorcontainer>
  </div>
</template>

<script>
import { DocumentEditorContainerComponent, Toolbar, DocumentEditor } from '@syncfusion/ej2-vue-documenteditor';
import { CollaborativeEditingHandler } from '@syncfusion/ej2-documenteditor';
import { HubConnectionBuilder, HttpTransportType, HubConnectionState } from '@microsoft/signalr';
import { hideSpinner, showSpinner } from '@syncfusion/ej2-popups';

export default {
  name: 'App',
  components: {
    'ejs-documenteditorcontainer': DocumentEditorContainerComponent
  },
  data() {
    return {
      serviceUrl: 'http://localhost:5212/api/documenteditor/',
      collborativeEditingServiceUrl: "http://localhost:5212/",
      collaborativeEditingHandler: null,
      connection: null,
    };
  },
  provide: {
    DocumentEditorContainer: [Toolbar]
  },
  methods: {

    onCreated() {
      DocumentEditor.Inject(CollaborativeEditingHandler);
      //Enable collaborative editing in Document Editor.
      this.$refs.doceditcontainer.ej2Instances.documentEditor.enableCollaborativeEditing = true;
      this.initializeSignalR();
      this.loadDocumentFromServer();
    },
    onContentChange(args) {
      if (this.collaborativeEditingHandler) {
        //Send the editing action to server
        this.collaborativeEditingHandler.sendActionToServer(args.operations)
      }
    },
    initializeSignalR() {
      // SignalR connection
      this.connection = new HubConnectionBuilder().withUrl(this.collborativeEditingServiceUrl + 'documenteditorhub', {
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
    },

    onDataRecived(action, data) {
      if (this.collaborativeEditingHandler) {
        if (action == 'connectionId') {
          //Update the current connection id to track other users
          this.connectionId = data;
        }
        //Apply the remote action in DocumentEditor
        this.collaborativeEditingHandler.applyRemoteAction(action, data);
      }
    },

    openDocument(responseText, roomName) {

      showSpinner(document.getElementById('spinner'));

      let data = JSON.parse(responseText);
      if (this.$refs.doceditcontainer) {

        this.collaborativeEditingHandler = this.$refs.doceditcontainer.ej2Instances.documentEditor.collaborativeEditingHandlerModule;
        //Update the room and version information to collaborative editing handler.
        this.collaborativeEditingHandler.updateRoomInfo(roomName, data.version, this.collborativeEditingServiceUrl + 'api/CollaborativeEditing/');

        //Open the document
        this.$refs.doceditcontainer.ej2Instances.documentEditor.open(data.sfdt);

        setTimeout(() => {
          if (this.$refs.doceditcontainer) {
            // connect to server using signalR
            this.connectToRoom({ action: 'connect', roomName: roomName, currentUser: this.$refs.doceditcontainer.currentUser });
          }
        });
      }
      hideSpinner(document.getElementById('spinner'));
    },

    loadDocumentFromServer() {
      const queryString = window.location.search;
      const urlParams = new URLSearchParams(queryString);
      let roomId = urlParams.get('id');
      if (roomId == null) {
        roomId = Math.random().toString(32).slice(2)
        window.history.replaceState({}, "", `?id=` + roomId);
      }
      var httpRequest = new XMLHttpRequest();
      httpRequest.open('Post', this.collborativeEditingServiceUrl + 'api/CollaborativeEditing/ImportFile', true);
      httpRequest.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
      httpRequest.onreadystatechange = () => {
        if (httpRequest.readyState === 4) {
          if (httpRequest.status === 200 || httpRequest.status === 304) {

            this.openDocument(httpRequest.responseText, roomId);
          }
          else {
            hideSpinner(document.getElementById('container'));
            alert('Fail to load the document');
          }
        }
      };
      httpRequest.send(JSON.stringify({ "fileName": "Giant Panda.docx", "roomName": roomId }));
    },
    connectToRoom(data) {
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
    }

  }
}
</script>
