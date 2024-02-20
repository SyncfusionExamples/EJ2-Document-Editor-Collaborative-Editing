<style>
@import '../node_modules/@syncfusion/ej2-base/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-buttons/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-inputs/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-vue-popups/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-lists/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-navigations/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-splitbuttons/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-dropdowns/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-layouts/styles/bootstrap5.css';
@import '../node_modules/@syncfusion/ej2-notifications/styles/bootstrap5.css';
@import "../node_modules/@syncfusion/ej2-vue-documenteditor/styles/bootstrap5.css";
</style>

<template>
  <div id="app-editor">
    <div id="toast_type"></div>
    <div style="height: 93vh;">
      <div ref="de_titlebar" id="documenteditor_titlebar" class="e-de-ctn-title">
        <div v-on:keydown="titleBarKeydownEvent" v-on:click="titleBarClickEvent" class="single-line"
          id="documenteditor_title_contentEditor" title="Document Name. Click or tap to rename this document."
          contenteditable="false">
          <label v-on:blur="titleBarBlurEvent" id="documenteditor_title_name" :style="titileStyle">{{ documentName
          }}</label>
        </div>
        <button type="button" v-on:click="shareBtnClick" title="Share this link"
          style="float: right; background: transparent; box-shadow: none; border-color: transparent; border-radius: 2px; color: inherit; font-size: 12px; text-transform: capitalize; margin-top: 4px; height: 28px; font-weight: 400; font-family: inherit;"
          class="e-control e-btn e-lib"><span class="e-btn-icon e-de-share e-icon-left"></span>Share</button>
      </div>
      <ejs-documenteditorcontainer id="container" ref="doceditcontainer" :height="height" :toolbarItems="toolbarItems"
        :currentUser="currentUser" :contentChange="onContentChange" :serviceUrl='serviceUrl' :enableToolbar='true'
        v-bind:created="onCreated">
      </ejs-documenteditorcontainer>
    </div>
    <div id="defaultDialog" style="display: none;">
      <div class="e-de-para-dlg-heading">
        Share this URL with others for real-time editing
      </div>
      <div class="e-de-container-row">
        <input type="text" id="share_url" class="e-input" readonly />
      </div>
    </div>
  </div>
</template>
<style>
#container {
  display: block;
}

.e-de-share::before {
  content: "\e7b9";
  font-family: 'e-icons';
}

.e-tooltip-menu-settings.e-tooltip-wrap.e-popup {
  background: white;
  border-radius: 0px;
  border: none;
}

.e-tooltip-menu-settings.e-tooltip-wrap.e-popup .collab-user-info {
  font-size: 12px;
  margin-left: 10px;
}

#documenteditor_titlebar {
  height: 36px;
  line-height: 26px;
  /* width: 100%; */
  font-size: 12px;
  padding-left: 15px;
  padding-right: 10px;
  /* font-family: inherit; */
}

#documenteditor_title_contentEditor {
  height: 26px;
  max-width: 85%;
  width: auto;
  overflow: hidden;
  display: inline-block;
  padding-left: 4px;
  padding-right: 4px;
  margin: 5px;
}

.single-line {
  cursor: text !important;
  outline: none;
}

.single-line:hover {
  border-color: #e4e4e4 !important;
}

[contenteditable="true"].single-line {
  white-space: nowrap;
  border-color: #e4e4e4 !important;
}

[contenteditable="true"].single-line * {
  white-space: nowrap;
}

/** Document editor sample level font icons*/

@font-face {
  font-family: 'Toast_icons';
  src: url(data:application/x-font-ttf;charset=utf-8;base64,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) format('truetype');
  font-weight: normal;
  font-style: normal;
}

.toast-icons {
  font-family: 'Toast_icons' !important;
  speak: none;
  font-size: 55px;
  font-style: normal;
  font-weight: normal;
  font-variant: normal;
  text-transform: none;
  line-height: 1;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

.toast-icons.e-success::before {
  content: "\e701";
}

.toast-icons.e-error::before {
  content: "\e700";
}
</style>

<script>
import { DocumentEditorContainerComponent, Toolbar, DocumentEditor } from '@syncfusion/ej2-vue-documenteditor';
import { CollaborativeEditingHandler } from '@syncfusion/ej2-documenteditor';
import { HubConnectionBuilder, HttpTransportType, HubConnectionState } from '@microsoft/signalr';
import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-vue-popups';
// import { ButtonComponent } from "@syncfusion/ej2-vue-buttons";
import { Tooltip } from '@syncfusion/ej2-popups';
import { Dialog } from '@syncfusion/ej2-popups';
import { Toast } from '@syncfusion/ej2-notifications';
import { ListView } from '@syncfusion/ej2-lists';
import { createElement } from '@syncfusion/ej2-base';

export default {
  name: 'App',
  components: {
    'ejs-documenteditorcontainer': DocumentEditorContainerComponent,
    // 'ejs-button': ButtonComponent
  },
  data() {
    return {
      serviceUrl: 'https://ej2services.syncfusion.com/production/web-services/api/documenteditor/',
      collborativeEditingServiceUrl: "https://webapplication120230413155843.azurewebsites.net/",
      collaborativeEditingHandler: null,
      connection: null,
      documentName: 'Gaint Panda',
      iconStyle: 'float:right;background: transparent;box-shadow:none;border-color: transparent;border-radius: 2px;color:inherit;font-size:12px;text-transform:capitalize;margin-top:4px;height:28px;font-weight:400;font-family:inherit;',
      titileStyle: 'text-transform:capitalize;font-weight:400;font-family:inherit;text-overflow:ellipsis;white-space:pre;overflow:hidden;user-select:none;cursor:text',
      printIconCss: 'e-de-share',
      userList: undefined,
      userMap: {},
      dialogObj: undefined,
      toastObj: undefined,
      connectionId: undefined,
      toolbarItems: ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields'],
      users: ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"],
      currentUser: '',
      height: '100%',
      buttonType: 'button',
    };
  },
  mounted() {
    this.$nextTick(() => {
      // Access the DOM element using this.$refs
      const titlebarRef = this.$refs.de_titlebar;
      // Check if the ref is available and is a valid DOM element
      if (titlebarRef instanceof Element) {
        this.userList = document.createElement('div');
        this.userList.id = 'de_userInfo';
        this.userList.style.float = 'right';
        this.userList.style.marginTop = '3px';
        if (this.userList) {
          titlebarRef.appendChild(this.userList);
        }
      }
      this.initDialog();
    });
  },

  provide: {
    DocumentEditorContainer: [Toolbar]
  },
  methods: {
    printBtnClick: function () {
      this.$refs.doceditcontainer.ej2Instances.documentEditor.print();
    },
    shareBtnClick: function () {
      console.log('shareBtnClick')
      this.dialogObj.show();
    },
    onCreated() {
      const random = Math.floor(Math.random() * this.users.length);
      this.currentUser = this.users[random];
      this.$refs.doceditcontainer.ej2Instances.documentEditor.documentName = 'Gaint Panda';
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
        } else if (this.connectionId != data.connectionId) {
          if (action == 'action' || action == 'addUser') {
            //Add the user to title bar when user joins the room
            this.updateUserInfo(data, 'addUser');
          } else if (action == 'removeUser') {
            //Remove the user from title bar when user leaves the room
            this.updateUserInfo(data, 'removeUser');
          }
        }
        //Apply the remote action in DocumentEditor
        this.collaborativeEditingHandler.applyRemoteAction(action, data);
      }
    },
    openDocument(responseText, roomName) {
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
      hideSpinner(document.getElementById('app-editor'));
    },
    loadDocumentFromServer() {
      createSpinner({
        // Specify the target for the spinner to show
        target: document.getElementById('app-editor')
      });
      showSpinner(document.getElementById('app-editor'));
      const queryString = window.location.search;
      const urlParams = new URLSearchParams(queryString);
      let roomId = urlParams.get('roomId');
      if (roomId == null) {
        roomId = Math.random().toString(32).slice(2)
        setTimeout(() => {
          window.history.replaceState({}, "", `?roomId=` + roomId);
        }, 200);
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
            hideSpinner(document.getElementById('app-editor'));
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
    },
    updateUserInfo(actionInfos, type) {
      if (!(actionInfos instanceof Array)) {
        actionInfos = [actionInfos];
      }
      if (type == "removeUser") {
        if (this.userMap[actionInfos]) {
          delete this.userMap[actionInfos];
        }
      } else {
        for (var j = 0; j < actionInfos.length; j++) {
          this.userMap[actionInfos[j].connectionId] = actionInfos[j];
        }
      }
      this.userList.innerHTML = "";
      let keys = Object.keys(this.userMap);
      for (var i = 0; i < keys.length; i++) {
        var actionInfo = this.userMap[keys[i]];
        var avatar = createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 5px', innerHTML: this.constructInitial(actionInfo.currentUser) });
        avatar.title = actionInfo.currentUser;
        avatar.style.cursor = 'default';
        this.userList.appendChild(avatar);
        if (keys.length > 5 && i == 4) {
          this.addListView(keys.slice(i + 1));
          break;
        }
      }
    },
    constructInitial(authorName) {
      const splittedName = authorName.split(' ');
      let initials = '';
      for (let i = 0; i < splittedName.length; i++) {
        if (splittedName[i].length > 0 && splittedName[i] !== '') {
          initials += splittedName[i][0];
        }
      }
      return initials;
    },
    addListView(keys) {
      var avatar = createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 3px', innerHTML: '+' + (keys.length) });
      avatar.style.cursor = 'pointer';
      avatar.tabIndex = 1;
      if (this.userList)
        this.userList.appendChild(avatar);
      var dataSource = [];
      for (var i = 0; i < keys.length; i++) {
        var actionInfo = this.userMap[keys[i]];
        dataSource.push({ id: "s_0" + i, text: actionInfo.currentUser, avatar: this.constructInitial(actionInfo.currentUser) });
      }
      var listViewContainer = document.createElement('div');
      var letterAvatarList = new ListView({
        // Bind listview datasource
        dataSource: dataSource,
        // Enable header title
        showHeader: false,
        // Assign list-item template
        template: '<div class="listWrapper">' +
          '${if(avatar!=="")}' +
          '<span class="e-avatar e-avatar-xsmall e-avatar-circle">${avatar}</span>' +
          '${else}' +
          '<span class="${pic} e-avatar e-avatar-xsmall e-avatar-circle"> </span>' +
          '${/if}' +
          '<span class="collab-user-info">' +
          '${text} </span> </div>',
        // Assign sorting order
        sortOrder: 'Ascending'
      });
      letterAvatarList.appendTo(listViewContainer);
      var listViewTooltip = new Tooltip({
        cssClass: 'e-tooltip-template-css e-tooltip-menu-settings',
        //Set tooltip open mode
        opensOn: 'Focus',
        //Set tooltip content
        content: listViewContainer,
        width: "200px",
        showTipPointer: false
      });
      //Render initialized Tooltip component
      listViewTooltip.appendTo(avatar);
    },
    initDialog() {
      this.dialogObj = new Dialog({
        header: 'Share ' + this.$refs.doceditcontainer.ej2Instances.documentEditor.documentName + '.docx',
        animationSettings: { effect: 'None' },
        showCloseIcon: true,
        isModal: true,
        width: '500px',
        visible: false,
        buttons: [{
          click: this.copyURL.bind(this),
          buttonModel: { content: 'Copy URL', isPrimary: true }
        }],
        open: function () {
          let urlTextBox = document.getElementById("share_url");
          if (urlTextBox) {
            urlTextBox.value = window.location.href;
            urlTextBox.select();
          }
        },
        beforeOpen: () => {
          if (this.dialogObj) {
            this.dialogObj.header = 'Share "' + this.$refs.doceditcontainer.ej2Instances.documentEditor.documentName + '.docx"';
          }
          let dialogElement = document.getElementById("defaultDialog");
          if (dialogElement) {
            dialogElement.style.display = "block";
          }
        },
      });
      this.dialogObj.appendTo('#defaultDialog');

      this.toastObj = new Toast({
        position: {
          X: 'Right'
        },
        target: document.body
      });
      this.toastObj.appendTo('#toast_type');
    },
    copyURL() {
      // Get the text field
      var copyText = document.getElementById("share_url");

      if (copyText) {
        // Select the text field
        copyText.select();
        copyText.setSelectionRange(0, 99999); // For mobile devices

        // Copy the text inside the text field
        navigator.clipboard.writeText(copyText.value);

        let toastMessage = { title: 'Success!', content: 'Link Copied.', cssClass: 'e-toast-success', icon: 'e-success toast-icons' };
        if (this.toastObj) {
          this.toastObj.show(toastMessage);
        }
        if (this.dialogObj) {
          this.dialogObj.hide();
        }
      }
    }
  },
}
</script>