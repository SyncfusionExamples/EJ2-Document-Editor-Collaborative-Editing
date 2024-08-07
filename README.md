# Collaborative Editing 

Allows multiple users to work on the same document simultaneously. This can be done in real-time, so that collaborators can see the changes as they are made. Collaborative editing can be a great way to improve efficiency, as it allows team members to work together on a document without having to wait for others to finish their changes.

## Prerequisites

- ***Real-time Transport Protocol***: This protocol facilitates instant communication between clients and the server, ensuring immediate updates during collaborative editing.

- ***Distributed Cache or Database***: Used to temporarily store the queue of editing operations.

### Real time transport protocol

- ***Managing Connections***: Keeps active connections open for real\-time collaboration, allowing seamless communication between users and the server.

- ***Broadcasting Changes***: Ensures that any edits made by one user are instantly sent to all collaborators, keeping everyone on the same page with the latest document version.

### Distributed cache or database

To support collaborative editing, it’s crucial to have a backing system that temporarily stores the editing operations of all active users. There are two primary options:

- ***Distributed Cache***: Handles more HTTP requests per second than a database. For example, a server with 2 vCPUs and 8GB RAM can process up to 125 requests per second using a distributed cache. We highly recommend using a distributed cache as a backing system over a database.

- ***Database***: With the same server configuration, it can handle up to 50 requests per second.

Using the distributed cache or database all the editing operations are queued in order and conflict resolution is performed using `Operational Transformation` algorithm.

>**Tips**: To calculate the average requests per second of your application Assume the editor in your live application is actively used by 1000 users and each user’s edit can trigger 2 to 5 requests per second. The total requests per second of your applications will be around 2000 to 5000. In this case, you can finalize a configuration to support around 5000 average requests per second.

>**Note**: The above metrics are based solely on the collaborative editing module. Actual throughput may decrease depending on other server-side interactions, such as document importing, pasting formatted content, editing restrictions, and spell checking. Therefore, it is advisable to monitor your app’s traffic and choose a configuration that best suits your needs.

# How our collaborative editing works 

## Client side

- For collaborative editing, each document requires a unique identifier. This identifier is used to create a room or topic where document changes are communicated.

- When a change is made in the editor, we construct a delta, which we refer to as operations, representing the changes. These operations are sent to the server using XMLHttpRequest. Subsequent changes by the current user are sent only after receiving a successful response from the XMLHttpRequest.

- Changes made to the same document by other users are received via a real\-time transport protocol.

## Server side

- Changes made by different users are ordered using a distributed cache or database, and a unique version number is updated based on the order in which changes are received by the server.

- Conflict resolution is performed using the Operational Transformation algorithm by comparing the client's last synced version with the current updated version.

- After conflict resolution, the distributed cache or database is updated with the latest changes.

- The changes are then broadcast to other users subscribed to the same room or topic using a real\-time transport protocol.

- Operations are saved to the source document periodically based on a defined maximum threshold limit.

## Collaborative editing Web API actions are supported in below platforms.

1. ASP.NET Core

2. ASP.NET MVC

3. Java

Please find the sample and documentation links below.

| **Client-side with dotnet** | **Server-side Web API** |
| :--- | :--- |
| <ul><li>[TypeScript](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20dotnet/TypeScript)</li><li>[JavaScript](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20dotnet/Javascript)</li><li>[Vue](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20dotnet/Vue)</li><li>[Angular](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20dotnet/Angular)</li><li>[React](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20dotnet/React)</li></ul> | **Using Distributed cache as backing system in ASP.NET Core** <table><tr><th> **ASP.NET Core Web API using** </th><th> **UG link**</th></tr><tr><td>[Redis](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Server%20side%20with%20distributed%20cache/ASP.NET%20Core/Using%20Redis)</td><td><ul><li>[TypeScript](https://ej2.syncfusion.com/documentation/document-editor/collaborative-editing/using-redis-cache-asp-net-core)</li><li>[JavaScript](https://ej2.syncfusion.com/javascript/documentation/document-editor/collaborative-editing/using-redis-cache-asp-net-core)</li><li>[Vue](https://ej2.syncfusion.com/vue/documentation/document-editor/collaborative-editing/using-redis-cache-asp-net-core)</li><li>[Angular](https://ej2.syncfusion.com/angular/documentation/document-editor/collaborative-editing/using-redis-cache-asp-net-core)</li><li>[React](https://ej2.syncfusion.com/react/documentation/document-editor/collaborative-editing/using-redis-cache-asp-net-core)</li></ul></td></tr></table> **Using database as backing system in ASP.NET Core** <table><tr><th> **ASP.NET Core Web API using** </th><th> **UG link** </th></tr><tr><td><ul><li>[MS SQL Server](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Server%20side%20with%20database/ASP.NET%20Core/Using%20MS%20SQL%20Server)</li><li>[PostgreSQL](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Server%20side%20with%20database/ASP.NET%20Core/Using%20PostgreSQL)</li><li>[My SQL Server](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Server%20side%20with%20database/ASP.NET%20Core/Using%20MySql%20Server)</li></ul></td><td><ul><li>[TypeScript](https://ej2.syncfusion.com/documentation/document-editor/collaborative-editing/using-dot-net)</li><li>[JavaScript](https://ej2.syncfusion.com/javascript/documentation/document-editor/collaborative-editing/using-dot-net)</li><li>[Vue](https://ej2.syncfusion.com/vue/documentation/document-editor/collaborative-editing/using-dot-net)</li><li>[Angular](https://ej2.syncfusion.com/angular/documentation/document-editor/collaborative-editing/using-dot-net)</li><li>[React](https://ej2.syncfusion.com/react/documentation/document-editor/collaborative-editing/using-dot-net)</li></ul></td></tr></table> |


|**Client\-side with Java**|**Server\-side Web API**|
|:---|:---|
| <ul><li>[TypeScript](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20Java/TypeScript)</li><li> [JavaScript](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20Java/JavaScript)</li><li>[Vue](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20Java/Vue) </li><li>[Angular](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20Java/Angular)</li><li>[React](https://github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Client%20side%20with%20Java/React)</li></ul>  |   **Using database as backing system** <table><tr><th>**Web API using**</th><th>**UG link**</th></tr><tr><td>[PostgreSQL](https://github.com/SyncfusionExamples/Ehttps:/github.com/SyncfusionExamples/EJ2-Document-Editor-Collaborative-Editing/tree/master/Server%20side%20with%20database/Java/Java%20web%20service%20using%20PostgreSQL)</td><td><ul><li>[TypeScript](https://ej2.syncfusion.com/documentation/document-editor/collaborative-editing/using-java)</li><li>[JavaScript](https://ej2.syncfusion.com/javascript/documentation/document-editor/collaborative-editing/using-java)</li><li>[Vue](https://ej2.syncfusion.com/vue/documentation/document-editor/collaborative-editing/using-java)</li><li>[Angular](https://ej2.syncfusion.com/angular/documentation/document-editor/collaborative-editing/using-java)</li><li>[React](https://ej2.syncfusion.com/react/documentation/document-editor/collaborative-editing/using-java)</li></ul></td></tr></table>|