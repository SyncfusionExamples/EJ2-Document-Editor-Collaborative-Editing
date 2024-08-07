import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-popups';

//Collaborative editing controller url

let serviceUrl = 'http://localhost:8024/';
var connectionId = "";
const toolbarItems = ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields']
const users = ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"];

const random = Math.floor(Math.random() * users.length);
var currentUser = users[random]
var currentRoomName = '';
var webSocketEndPoint = 'http://localhost:8024/ws';
var stompClient;

/**
 * Container component
 */
var container = new ej.documenteditor.DocumentEditorContainer({ height: "100%", toolbarItems: toolbarItems, enableToolbar: true, currentUser: currentUser });
container.serviceUrl = serviceUrl + 'api/wordeditor/';
ej.documenteditor.DocumentEditorContainer.Inject(ej.documenteditor.Toolbar);
container.appendTo('#documenteditor');

//Injecting collaborative editing module
ej.documenteditor.DocumentEditor.Inject(ej.documenteditor.CollaborativeEditingHandler);
//Enable collaborative editing in DocumentEditor
container.documentEditor.enableCollaborativeEditing = true;

container.contentChange = function (args) {
    if (container.documentEditor.collaborativeEditingHandlerModule) {
        container.documentEditor.collaborativeEditingHandlerModule.sendActionToServer(args.operations);
    }
};

window.addEventListener('resize', function (e) {
    setTimeout(() => {
        container.resize();
    }, 0);
});

function initializeSockJs() {
    let ws = new SockJS(webSocketEndPoint);
    stompClient = Stomp.over(ws);
    stompClient.connect({}, () => {
        onConnected();
    }, (error) => {
        console.error('Error during WebSocket connection', error);
    });
}

function onConnected() {
    if (stompClient.connected) {
        // Subscribe to the specific topic            
        stompClient.subscribe('/topic/public/' + currentRoomName, onDataRecived);
        connectToRoom(currentRoomName);
        console.log('server connected!!!');
    } else {
        console.log('Waiting for WebSocket connection...');
    }
}

//Method to process the data received from server
function onDataRecived(data) {
    if (container.documentEditor.collaborativeEditingHandlerModule) {
        var content = JSON.parse(data.body);
        var action = content.headers['action'];
        debugger;

        if (content.payload.operations != null) {
            container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("action", content.payload);
        }
        else if (action == "removeUser") {
            container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("removeUser", content.payload.connectionId);
            titleBar.updateUserInfo(content.payload.connectionId,'removeUser');
        } else if (action == "connectionId" && connectionId == "") {
            connectionId = content.payload.connectionId;
            container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction("connectionId", content.payload.connectionId);
        }
        else if (Array.isArray(content.payload)) {
            titleBar.updateUserInfo(content.payload, 'addUser');
        }
    }
}

function connectToRoom() {
    var userInfo = {
        currentUser: container.currentUser,
        clientVersion: 0,
        roomName: currentRoomName,
        connectionId: "",
    };
    //Send the user information to server
    stompClient.send("/app/join/" + currentRoomName, {}, JSON.stringify(userInfo));
};

function openDocument(responseText, roomName) {


    var data = JSON.parse(responseText);

    //Update the room and version information to collaborative editing handler.
    container.documentEditor.collaborativeEditingHandlerModule.updateRoomInfo(roomName, data.version, serviceUrl + 'api/collaborativeediting/');

    //Open the document
    container.documentEditor.open(data.sfdt);

    container.documentEditor.documentName = "Giant Panda";
    setTimeout(function () {
        // connect to server using signalR
        connectToRoom({ action: 'connect', roomName: roomName, currentUser: container.currentUser });
    });

    hideSpinner(document.body);
    titleBar.updateDocumentTitle();
}

function loadDocumentFromServer() {
    showSpinner(document.body);
    var queryString = window.location.search;
    var urlParams = new URLSearchParams(queryString);
    //Get the room id from the query string
    var roomId = urlParams.get('roomId');

    if (roomId == null) {
        // If room id is not available, generate a new room id
        // This random is used for demo purpose only. In real-time scenario, you need to generate a unique room id for each document and maintain it in the server and 
        // reuse it when the user reopens the document.
        roomId = Math.random().toString(32).slice(2)
        window.history.replaceState({}, "", `?roomId=` + roomId);
    }
    currentRoomName = roomId;
    var httpRequest = new XMLHttpRequest();
    httpRequest.open('Post', serviceUrl + 'api/collaborativeediting/ImportFile', true);
    httpRequest.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    httpRequest.onreadystatechange = function () {
        if (httpRequest.readyState === 4) {
            if (httpRequest.status === 200 || httpRequest.status === 304) {
                openDocument(httpRequest.responseText, roomId);
            } else {
                hideSpinner(document.body);
                alert('Fail to load the document');
            }
        }
    };
    httpRequest.send(JSON.stringify({ "fileName": "Giant Panda.docx", "roomName": roomId }));
}

createSpinner({
    target: document.body
});

initializeSockJs();
loadDocumentFromServer();

// title bar
var TitleBar = function () {
    function TitleBar(element, docEditor, isShareNeeded, isRtl) {
        var _this = this;
        this.userMap = {};
        this.initializeTitleBar = function (isShareNeeded) {
            var shareText;
            var shareToolTip;
            var documentTileText;
            if (!_this.isRtl) {
                shareText = 'Share';
                shareToolTip = 'Share this link';
            }
            _this.documentTitle = ej.base.createElement('label', { id: 'documenteditor_title_name', className: "e-control", styles: 'font-weight:400;text-overflow:ellipsis;white-space:pre;overflow:hidden;user-select:none;cursor:text;font-size:14px' });
            var iconCss = 'e-de-padding-right';
            var btnFloatStyle = 'float:right;';
            var titleCss = '';
            _this.documentTitleContentEditor = ej.base.createElement('div', { id: 'documenteditor_title_contentEditor', className: 'single-line', styles: titleCss });
            _this.documentTitleContentEditor.appendChild(_this.documentTitle);
            _this.tileBarDiv.appendChild(_this.documentTitleContentEditor);
            _this.documentTitleContentEditor.setAttribute('title', documentTileText);
            var btnStyles = btnFloatStyle + 'background: transparent;box-shadow:none;border-color: transparent;'
                + 'border-radius: 2px;color:inherit;font-size:12px;text-transform:capitalize;height:28px;font-weight:400;margin-top: 2px;';
            _this.print = _this.addButton(iconCss, shareText, btnStyles, 'de-print', shareToolTip, false);
            _this.userList = ej.base.createElement('div', { id: 'de_userInfo', styles: 'float:right;margin-top: 3px;' });
            _this.tileBarDiv.appendChild(_this.userList);
        };
        this.wireEvents = function () {
            _this.print.element.addEventListener('click', _this.shareUrl);
        };
        this.shareUrl = function () {
            dialogObj.show();
        },
            this.updateDocumentTitle = function () {
                if (_this.documentEditor.documentName === '') {
                    _this.documentEditor.documentName = 'Untitled';
                }
                _this.documentTitle.textContent = _this.documentEditor.documentName;
            };
        this.onPrint = function () {
            _this.documentEditor.print();
        };
        this.tileBarDiv = element;
        this.documentEditor = docEditor;
        this.isRtl = isRtl;
        this.initializeTitleBar(isShareNeeded);
        this.wireEvents();

    }
    TitleBar.prototype.addButton = function (iconClass, btnText, styles, id, tooltipText, isDropDown, items) {
        var button = ej.base.createElement('button', { id: id, styles: styles });
        this.tileBarDiv.appendChild(button);
        button.setAttribute('title', tooltipText);
        var ejButton = new ej.buttons.Button({ content: btnText, iconCss: 'e-de-share' }, button);
        return ejButton;
    };
    TitleBar.prototype.updateUserInfo = function (actionInfos, type) {
        if (!(actionInfos instanceof Array)) {
            actionInfos = [actionInfos];
        }
        if (type == "removeUser") {
            if (this.userMap[actionInfos]) {
                delete this.userMap[actionInfos];
            }
        } else {
            for (var i = 0; i < actionInfos.length; i++) {
                this.userMap[actionInfos[i].connectionId] = actionInfos[i];
            }
        }
        this.userList.innerHTML = "";
        let keys = Object.keys(this.userMap);
        for (var i = 0; i < keys.length; i++) {
            var actionInfo = this.userMap[keys[i]];
            var avatar = ej.base.createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 5px', innerHTML: this.constructInitial(actionInfo.currentUser) });
            avatar.title = actionInfo.currentUser;
            avatar.style.cursor = 'default';
            this.userList.appendChild(avatar);
            if (keys.length > 5 && i == 4) {
                this.addListView(keys.slice(i + 1));
                break;
            }
        }
    };
    TitleBar.prototype.addListView = function (keys) {
        var avatar = ej.base.createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 3px', innerHTML: '+' + (keys.length) });
        avatar.style.cursor = 'pointer';
        avatar.tabIndex = 1;
        this.userList.appendChild(avatar);
        var dataSource = [];
        for (var i = 0; i < keys.length; i++) {
            var actionInfo = this.userMap[keys[i]];
            dataSource.push({ id: "s_0" + i, text: actionInfo.currentUser, avatar: this.constructInitial(actionInfo.currentUser) });
        }
        var listViewContainer = document.createElement('div');
        var letterAvatarList = new ej.lists.ListView({
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
        var listViewTooltip = new ej.popups.Tooltip({
            cssClass: 'e-tooltip-template-css',
            //Set tooltip open mode
            opensOn: 'Focus',
            //Set tooltip content
            content: listViewContainer,
            width: "200px",
            cssClass: 'e-tooltip-menu-settings',
            showTipPointer: false
        });
        //Render initialized Tooltip component
        listViewTooltip.appendTo(avatar);
    };
    TitleBar.prototype.constructInitial = function (authorName) {
        var splittedName = authorName.split(' ');
        var initials = '';
        for (var i = 0; i < splittedName.length; i++) {
            if (splittedName[i].length > 0 && splittedName[i] !== '') {
                initials += splittedName[i][0];
            }
        }
        return initials;


    };
    return TitleBar;
}();

var titleBar = new TitleBar(document.getElementById('documenteditor_titlebar'), container.documentEditor, true);


var dialogObj = new ej.popups.Dialog({
    header: 'Share ' + container.documentEditor.documentName + '.docx',
    animationSettings: { effect: 'None' },
    showCloseIcon: true,
    isModal: true,
    width: '500px',
    visible: false,
    buttons: [{
        click: dlgButtonClick,
        buttonModel: { id: "copy_button", content: 'Copy URL', isPrimary: true }
    }],
    open: function () {
        document.getElementById("share_url").value = window.location.href;
        document.getElementById("share_url").select();
    },
    beforeOpen: function () {
        dialogObj.header = 'Share "' + container.documentEditor.documentName + '.docx"';
        document.getElementById("defaultDialog").style.display = "block";
    },
});
dialogObj.appendTo('#defaultDialog');

function dlgButtonClick(event) {
    // Get the text field
    var copyText = document.getElementById("share_url");

    // Select the text field
    copyText.select();
    copyText.setSelectionRange(0, 99999); // For mobile devices

    // Copy the text inside the text field
    navigator.clipboard.writeText(copyText.value);

    let toastMessage = { title: 'Success!', content: 'Link Copied.', cssClass: 'e-toast-success', icon: 'e-success toast-icons' };
    toastObj.show(toastMessage);
    dialogObj.hide();
}


var toastObj = new ej.notifications.Toast({
    position: {
        X: 'Right'
    },
    target: document.body
});
toastObj.appendTo('#toast_type');

