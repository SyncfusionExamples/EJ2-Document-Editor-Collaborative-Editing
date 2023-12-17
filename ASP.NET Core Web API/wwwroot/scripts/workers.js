importScripts('./signalr.js');

self.addEventListener('message', function (e) {
    if (e.data.action === 'connect') {
        start(e.data);
    }
}, false);

var currentRoomName;

//var serviceUrl = "http://localhost:5212/";
var serviceUrl = "https://webapplication120230413155843.azurewebsites.net/";

var connection = new signalR.HubConnectionBuilder().withUrl(serviceUrl + 'documenteditorhub', {
    skipNegotiation: true,
    transport: signalR.HttpTransportType.WebSockets
}).withAutomaticReconnect().build();

// function connect(data) {
//     currentRoomName = data.roomName;
//     connection.start().then(function () {
//         connection.send('JoinGroup', { fileName: currentRoomName, currentUser: data.currentUser });
//         console.log('server connected!!!');
//     });
// }

connection.onclose(async () => {
    if (connection.state === signalR.HubConnectionState.Disconnected) {
        alert('Connection lost. Please relod the browser to continue.');
    }
    //Reconnect
    //await start();
});

async function start(data) {
    try {
        currentRoomName = data.roomName;
        connection.start().then(function () {
            connection.send('JoinGroup', { roomName: currentRoomName, currentUser: data.currentUser });
            console.log('server connected!!!');
        });
    } catch (err) {
        console.log(err);
        setTimeout(start, 5000);
    }
};

connection.on('dataReceived', onDataRecived.bind(this));

function onDataRecived(action, data) {
    self.postMessage({ action: action, data: data }, null);
}
