import { createSpinner, hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import { HubConnectionBuilder, HttpTransportType, HubConnectionState } from '@microsoft/signalr';

//Collaborative editing controller url

var serviceUrl = 'http://localhost:5212/';
var collborativeEditingHandler;
var connectionId = "";
const toolbarItems = ['Undo', 'Redo', 'Separator', 'Image', 'Table', 'Hyperlink', 'Bookmark', 'TableOfContents', 'Separator', 'Header', 'Footer', 'PageSetup', 'PageNumber', 'Break', 'InsertFootnote', 'InsertEndnote', 'Separator', 'Find', 'Separator', 'Comments', 'TrackChanges', 'Separator', 'LocalClipboard', 'RestrictEditing', 'Separator', 'FormFields', 'UpdateFields']
const users = ["Kathryn Fuller", "Tamer Fuller", "Martin Nancy", "Davolio Leverling", "Nancy Fuller", "Fuller Margaret", "Leverling Andrew"];

const random = Math.floor(Math.random() * users.length);
var currentUser = users[random]

/**
 * Container component
 */
var container = new ej.documenteditor.DocumentEditorContainer({ height: "100%", toolbarItems: toolbarItems, enableToolbar: true, currentUser: currentUser });
container.serviceUrl = serviceUrl + 'api/documenteditor/';
ej.documenteditor.DocumentEditorContainer.Inject(ej.documenteditor.Toolbar);
container.appendTo('#documenteditor');

//Injecting collaborative editing module
ej.documenteditor.DocumentEditor.Inject(ej.documenteditor.CollaborativeEditingHandler);
//Enable collaborative editing in DocumentEditor
container.documentEditor.enableCollaborativeEditing = true;

container.contentChange = function (args) {
    if (collborativeEditingHandler) {
        collborativeEditingHandler.sendActionToServer(args.operations);
    }
};

window.addEventListener('resize', function (e) { 
    setTimeout(() => {
        container.resize();
    }, 0);
});

// SignalR connection
var connection = new HubConnectionBuilder().withUrl(serviceUrl + 'documenteditorhub', {
    skipNegotiation: true,
    transport: HttpTransportType.WebSockets
}).withAutomaticReconnect().build();

async function connectToRoom(data) {
    try {
        // start the connection.
        connection.start().then(function () {
            // Join the room.
            connection.send('JoinGroup', { roomName: data.roomName, currentUser: data.currentUser });
            console.log('server connected!!!');
        });
    } catch (err) {
        console.log(err);
        //Attempting to reconnect in 5 seconds
        setTimeout(connectToRoom, 5000);
    }
};

//Event handler for signalR connection
connection.on('dataReceived', onDataRecived.bind(this));

//Method to process the data received from server
function onDataRecived(action, data) {
    if (collborativeEditingHandler) {
        if (action == 'connectionId') {
            //Update the current connection id to track other users
            connectionId = data;
        } else if (connectionId != data.connectionId) {
            if (action == 'action' || action == 'addUser') {
                //Add the user to title bar when user joins the room
                titleBar.updateUserInfo(data, 'addUser');
            } else if (action == 'removeUser') {
                //Remove the user from title bar when user leaves the room
                titleBar.updateUserInfo(data, 'removeUser');
            }
        }
        //Apply the remote action in DocumentEditor
        collborativeEditingHandler.applyRemoteAction(action, data);
    }
}

connection.onclose(async () => {
    if (connection.state === HubConnectionState.Disconnected) {
        alert('Connection lost. Please reload the browser to continue.');
    }
});

function openDocument(responseText, roomName) {
   

    var data = JSON.parse(responseText);

    collborativeEditingHandler = container.documentEditor.collaborativeEditingHandlerModule;
    //Update the room and version information to collaborative editing handler.
    collborativeEditingHandler.updateRoomInfo(roomName, data.version, serviceUrl + 'api/CollaborativeEditing/');

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
    var roomId = urlParams.get('roomId');

    if (roomId == null) {
        roomId = Math.random().toString(32).slice(2)
        window.history.replaceState({}, "", `?roomId=` + roomId);
    }
    var httpRequest = new XMLHttpRequest();
    httpRequest.open('Post', serviceUrl + 'api/CollaborativeEditing/ImportFile', true);
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

