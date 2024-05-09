# How to host the DocumentEditor service.

Opening and saving documents from S3 bucket using PostgreSQL.

## Maven Project

Please find the list of commands used for running and deploying the spring boot application in Azure.

Clean the package using

> mvn clean package

Run the application locally using

> mvn spring-boot:run

Build the package using

> mvn package

Above package generation command creates the "tomcat-0.0.1-SNAPSHOT.war" in the below location.

> target\tomcat-0.0.1-SNAPSHOT.war

Please create a Azure app service with Java & Tomcat.

After creating the app service 

>Development Tools -> Advanced Tools -> Go -> Debug console -> CMD

Once the file manager is opened please navigate to

>site -> wwwroot -> webapps

Upload the generated war file "tomcat-0.0.1-SNAPSHOT.war" the application will be hosted under

>{site-name}/tomcat-0.0.1-SNAPSHOT

