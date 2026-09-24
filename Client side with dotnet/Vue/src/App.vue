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
  <div>
    <div ref="de_titlebar" id="documenteditor_titlebar" class="e-de-ctn-title">
      <ejs-button id="de-print" :style="iconStyle" :iconCss="printIconCss" v-on:click="shareBtnClick"
        title="Print this document (Ctrl+P).">Print</ejs-button>
    </div>
    <div id="spinner">
      <ejs-documenteditorcontainer ref="doceditcontainer" :serviceUrl='serviceUrl'
        :enableToolbar='true' v-bind:created="onCreated">
      </ejs-documenteditorcontainer>
    </div>
    <div id="tooltipContent" style="display:none">
      <div class="content">
        <div style="margin-bottom:12px;font-size:15px">Share this URL with other for realtime editing</div>
        <div style="display:flex">
          <input id="share_url" type="text" class="e-input" />
          <button style="margin-left:10px" v-on:click="copyUrl" class="e-primary e-btn ">Copy Url</button>
        </div>
      </div>
    </div>
  </div>
</template>
<style>
#documenteditor_titlebar {
  height: 36px;
  line-height: 26px;
  width: 100%;
  font-size: 12px;
  padding-left: 15px;
  padding-right: 10px;
  font-family: inherit;
}
</style>

<script>
import { DocumentEditorContainerComponent, Toolbar, DocumentEditor } from '@syncfusion/ej2-vue-documenteditor';
import { CollaborativeEditingHandler } from '@syncfusion/ej2-documenteditor';
import { Tooltip } from '@syncfusion/ej2-popups';
import { ButtonComponent } from "@syncfusion/ej2-vue-buttons";
import { CollaborationClient } from "@syncfusion/ej2-collaborator";
import { DocumentEditorAdapter } from "./Collaborator/DocumentEditorAdapter";
export default {
  name: 'App',
  components: {
    'ejs-documenteditorcontainer': DocumentEditorContainerComponent,
    'ejs-button': ButtonComponent
  },
  data() {
    return {
      serviceUrl: 'http://localhost:5212/',
      collborativeEditingServiceUrl: 'http://localhost:5212/',    
      connection: null,
      documentName: 'Getting Started',
      documentTitle: 'Untitled Document',
      iconStyle: 'float:right;background: transparent;box-shadow:none;border-color: transparent;border-radius: 2px;color:inherit;font-size:12px;text-transform:capitalize;margin-top:4px;height:28px;font-weight:400;font-family:inherit;',
      titileStyle: 'text-transform:capitalize;font-weight:400;font-family:inherit;text-overflow:ellipsis;white-space:pre;overflow:hidden;user-select:none;cursor:text',
      printIconCss: 'e-de-icon-Print e-de-padding-right',
      userList: undefined,
      userMap: {},
      currentRoomName : ''
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
    });
  },

  provide: {
    DocumentEditorContainer: [Toolbar]
  },
  methods: {

    onCreated() {
      DocumentEditor.Inject(CollaborativeEditingHandler);
      //Enable collaborative editing in Document Editor.
      this.$refs.doceditcontainer.ej2Instances.documentEditor.enableCollaborativeEditing = true;
      const adapter = new DocumentEditorAdapter( this.$refs.doceditcontainer.ej2Instances, 'http://localhost:5212/');
      const client = new CollaborationClient(adapter, {
      serviceUrl:'http://localhost:5212/', //ASP.NET Core + SignalR
      currentUser: "GUest User",
      connectionType: "siganlr",      
   
      onUserJoined: (user) => {
          console.log("User Joined", user);
            this.connectionId = user;
            this.addUser(user);
      },
      onUserLeft: (user) => {
          console.log("User Left", user);
          this.removeUser(user);
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
    },
   
    
    onDataRecived(action, data) {
      if (this.$refs.doceditcontainer.ej2Instances.documentEditor.collaborativeEditingHandlerModule) {
        if (action == 'connectionId') {
          //Update the current connection id to track other users
          this.connectionId = data;
          this.addUser(data);
        }
        //Apply the remote action in DocumentEditor
        this.$refs.doceditcontainer.ej2Instances.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction(action, data);
        this.removeUser(data);
      }
    },

    
    addUser(actionInfos) {
      if (!(actionInfos instanceof Array)) {
        actionInfos = [actionInfos];
      }

      for (let i = 0; i < actionInfos.length; i++) {
        let actionInfo = actionInfos[i];
        if (this.userMap[actionInfo.connectionId]) {
          continue;
        }

        let avatar = document.createElement('div');
        avatar.className = 'e-avatar e-avatar-xsmall e-avatar-circle';
        avatar.style.margin = '0px 5px';
        avatar.innerHTML = this.constructInitial(actionInfo.currentUser);
        if (this.userList) {
          this.userList.appendChild(avatar);
        }
        this.$set(this.userMap, actionInfo.connectionId, avatar);
      }
    },

    removeUser(connectionId) {
      if (this.userMap[connectionId]) {
        if (this.userList) {
          // Assuming you are directly manipulating the DOM
          const avatarElement = this.userList.querySelector(`[data-connection-id="${connectionId}"]`);
          if (avatarElement) {
            this.userList.removeChild(avatarElement);
          }
        }
        this.$delete(this.userMap, connectionId);
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

    printBtnClick: function () {
      this.$refs.doceditcontainer.ej2Instances.documentEditor.print();
    },

    shareBtnClick: function () {
    },

    // Title bar share.
    createTooltip() {
        let tooltip = new Tooltip({
            cssClass: 'e-tooltip-template-css',
            opensOn: 'Click Custom Focus',
            content: document.getElementById("tooltipContent"),
            beforeRender: this.onBeforeRender,
            afterOpen: this.onAfterOpen,
            width: '400px'
        });
        tooltip.appendTo('#de-print');
    },
    onBeforeRender() {
        if (document.getElementById('tooltipContent')) {
            document.getElementById('tooltipContent').style.display = 'block';
        }
    },

    onAfterOpen() {
      document.getElementById("share_url").value = window.location.href;
    },

    copyUrl: function () {
      // Get the text field
      var copyText = document.getElementById("share_url");

      // Select the text field
      copyText.select();
      copyText.setSelectionRange(0, 99999); // For mobile devices

      // Copy the text inside the text field
      navigator.clipboard.writeText(copyText.value);
    },
  },
}
</script>
