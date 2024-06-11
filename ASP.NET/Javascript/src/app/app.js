import { hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import { HubConnectionBuilder, HttpTransportType, HubConnectionState } from '@microsoft/signalr';

//Collaborative editing controller url

var serviceUrl = 'http://localhost:5212/';
var connectionId = "";
var currentRoomName = '';
/**
 * Container component
 */
var container = new ej.documenteditor.DocumentEditorContainer({ height: "590px", enableToolbar: true, showPropertiesPane: false, currentUser: 'Guest User' });
container.serviceUrl = serviceUrl + 'api/documenteditor/';
ej.documenteditor.DocumentEditorContainer.Inject(ej.documenteditor.Toolbar);
container.appendTo('#container');

//Injecting collaborative editing module
ej.documenteditor.DocumentEditor.Inject(ej.documenteditor.CollaborativeEditingHandler);
//Enable collaborative editing in DocumentEditor
container.documentEditor.enableCollaborativeEditing = true;

container.documentEditor.documentName = 'Getting Started';

container.contentChange = function (args) {
    if (container.documentEditor.collaborativeEditingHandlerModule) {
        container.documentEditor.collaborativeEditingHandlerModule.sendActionToServer(args.operations);
    }
};


// SignalR connection
var connection = new HubConnectionBuilder().withUrl(serviceUrl + 'documenteditorhub', {
    skipNegotiation: true,
    transport: HttpTransportType.WebSockets
}).withAutomaticReconnect().build();

async function connectToRoom(data) {
    try {
        currentRoomName = data.roomName;
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
    if (container.documentEditor.collaborativeEditingHandlerModule) {
        if (action == 'connectionId') {
            //Update the current connection id to track other users
            connectionId = data;
        } else if (connectionId != data.connectionId) {
            if (action == 'action' || action == 'addUser') {
                //Add the user to title bar when user joins the room
                titleBar.addUser(data);
            } else if (action == 'removeUser') {
                //Remove the user from title bar when user leaves the room
                titleBar.removeUser(data);
            }
        }
        //Apply the remote action in DocumentEditor
        container.documentEditor.collaborativeEditingHandlerModule.applyRemoteAction(action, data);
    }
}

connection.onclose(async () => {
    if (connection.state === HubConnectionState.Disconnected) {
        alert('Connection lost. Please reload the browser to continue.');
    }
});

connection.onreconnected(() => {
    if (currentRoomName != null) {
        connection.send('JoinGroup', { roomName: currentRoomName, currentUser: currentUser });
    }
    console.log('server reconnected!!!');
});

function openDocument(responseText, roomName) {
    showSpinner(document.getElementById('container'));

    var data = JSON.parse(responseText);   
    //Update the room and version information to collaborative editing handler.
    container.documentEditor.collaborativeEditingHandlerModule.updateRoomInfo(roomName, data.version, serviceUrl + 'api/CollaborativeEditing/');

    //Open the document
    container.documentEditor.open(data.sfdt);

    container.documentEditor.documentName = "Giant Panda";
    setTimeout(function () {
        // connect to server using signalR
        connectToRoom({ action: 'connect', roomName: roomName, currentUser: container.currentUser });
    });

    hideSpinner(document.getElementById('container'));
}

function loadDocumentFromServer() {
    var queryString = window.location.search;
    var urlParams = new URLSearchParams(queryString);
    var roomId = urlParams.get('id');

    if (roomId == null) {
        roomId = Math.random().toString(32).slice(2)
        window.history.replaceState({}, "", `?id=` + roomId);
    }
    var httpRequest = new XMLHttpRequest();
    httpRequest.open('Post', serviceUrl + 'api/CollaborativeEditing/ImportFile', true);
    httpRequest.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    httpRequest.onreadystatechange = function () {
        if (httpRequest.readyState === 4) {
            if (httpRequest.status === 200 || httpRequest.status === 304) {
                openDocument(httpRequest.responseText, roomId);
            } else {
                hideSpinner(document.getElementById('container'));
                alert('Fail to load the document');
            }
        }
    };
    httpRequest.send(JSON.stringify({ "fileName": "Giant Panda.docx", "roomName": roomId }));
}

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
            _this.documentTitle = ej.base.createElement('label', { id: 'documenteditor_title_name', styles: 'font-weight:400;text-overflow:ellipsis;white-space:pre;overflow:hidden;user-select:none;cursor:text' });
            var iconCss = 'e-de-padding-right';
            var btnFloatStyle = 'float:right;';
            var titleCss = '';
            _this.documentTitleContentEditor = ej.base.createElement('div', { id: 'documenteditor_title_contentEditor', className: 'single-line', styles: titleCss });
            _this.documentTitleContentEditor.appendChild(_this.documentTitle);
            _this.tileBarDiv.appendChild(_this.documentTitleContentEditor);
            _this.documentTitleContentEditor.setAttribute('title', documentTileText);
            var btnStyles = btnFloatStyle + 'background: transparent;box-shadow:none; font-family: inherit;border-color: transparent;'
                + 'border-radius: 2px;color:inherit;font-size:12px;text-transform:capitalize;height:28px;font-weight:400;margin-top: 2px;';
            _this.print = _this.addButton('e-de-icon-Print ' + iconCss, shareText, btnStyles, 'de-print', shareToolTip, false);
            _this.userList = ej.base.createElement('div', { id: 'de_userInfo', styles: 'float:right;margin-top: 3px;' });
            _this.tileBarDiv.appendChild(_this.userList);
        };
        this.wireEvents = function () {
            _this.print.element.addEventListener('click', _this.shareUrl);
        };
        this.shareUrl = function () {

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
        var ejButton = new ej.buttons.Button({ iconCss: iconClass, content: btnText }, button);
        return ejButton;
    };
    TitleBar.prototype.addUser = function (actionInfos) {
        if (!(actionInfos instanceof Array)) {
            actionInfos = [actionInfos];
        }
        for (var i = 0; i < actionInfos.length; i++) {
            var actionInfo = actionInfos[i];
            if (this.userMap[actionInfo.connectionId]) {
                continue;
            }
            var avatar = ej.base.createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 5px', innerHTML: this.constructInitial(actionInfo.currentUser) });
            this.userMap[actionInfo.connectionId] = avatar;
            avatar.title = actionInfo.currentUser;
            this.userList.appendChild(avatar);
        }
    };
    TitleBar.prototype.removeUser = function (conectionId) {
        if (this.userMap[conectionId]) {
            this.userList.removeChild(this.userMap[conectionId]);
            delete this.userMap[conectionId];
        }
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

var tooltip = new ej.popups.Tooltip({
    cssClass: 'e-tooltip-template-css',
    //Set tooltip open mode
    opensOn: 'Click Custom Focus',
    //Set tooltip content
    content: createPopUpDisplay(),
    beforeRender: onBeforeRender,
    afterOpen: onAfterOpen,
    width: "400px"
});
//Render initialized Tooltip component
tooltip.appendTo('#de-print');

function onBeforeRender() {
    if (document.getElementById('tooltipContent')) {
        document.getElementById('tooltipContent').style.display = 'block';
    }
}
function onAfterOpen() {
    document.getElementById("share_url").value = window.location.href;
}

function copyUrl() {
    // Get the text field
    var copyText = document.getElementById("share_url");

    // Select the text field
    copyText.select();
    copyText.setSelectionRange(0, 99999); // For mobile devices

    // Copy the text inside the text field
    navigator.clipboard.writeText(copyText.value);
}
function createPopUpDisplay() {
    //Creatin the copy link element
    var tooltip = ej.base.createElement('div', { id: 'tooltipContent', styles: 'display:none' });
    var tooltipClass = ej.base.createElement('div', { className: 'content' });
    var firstChild = ej.base.createElement('div', { styles: 'margin-bottom:12px;font-size:15px', })
    firstChild.textContent = 'Share this URL with other for realtime editing';
    var secondchild = ej.base.createElement('div', { styles: 'display:flex' });
    secondchild.appendChild(ej.base.createElement('input', { id: 'share_url', className: 'e-input', attrs: { type: 'text' } }));
    var copyButton = ej.base.createElement('button', {
        styles: 'margin-left:10px',
        className: 'e-primary e-btn',
        innerHTML: 'Copy Url'
    });
    copyButton.addEventListener('click', copyUrl);
    secondchild.appendChild(copyButton);
    tooltipClass.appendChild(firstChild);
    tooltipClass.appendChild(secondchild);
    return tooltip.appendChild(tooltipClass);
}
