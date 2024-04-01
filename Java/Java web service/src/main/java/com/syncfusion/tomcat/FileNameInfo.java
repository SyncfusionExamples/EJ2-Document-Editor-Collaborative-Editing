package com.syncfusion.tomcat;

import java.awt.List;
import java.util.ArrayList;

import com.syncfusion.ej2.wordprocessor.ActionInfo;
import com.syncfusion.tomcat.controller.DocumentEditorHub;

public class FileNameInfo {

	
	private int fileIndex;
	private String fileName;

    public FileNameInfo(int index, String fileName)
    {
        this.setFileIndex(index);
        this.setFileName(fileName);
        if (DocumentEditorHub.roomList.containsKey(fileName)) {
            ArrayList<ActionInfo> users = DocumentEditorHub.roomList.get(fileName);
            for (ActionInfo user : users) {
                activeUsers.add(constructInitials(user.getCurrentUser()));
            }
        }
    }
    public String constructInitials(String authorName) {
        String[] splittedName = authorName.split(" ");
        StringBuilder initials = new StringBuilder();
        for (String namePart : splittedName) {
            if (namePart.length() > 0 && !namePart.isEmpty()) {
                initials.append(namePart.charAt(0));
            }
        }
        return initials.toString();
    }

	public String getFileName() {
		return fileName;
	}

	public void setFileName(String fileName) {
		this.fileName = fileName;
	}

	public int getFileIndex() {
		return fileIndex;
	}

	public void setFileIndex(int fileIndex) {
		this.fileIndex = fileIndex;
	}
	 private String documentName;
	    private String createdOn;
	    private String sharedWith;
	    private String documentID;
	    private String sharedBy;
	    private String owner;
	    private ArrayList<String> activeUsers = new ArrayList<>();

	    public String getDocumentName() {
	        return documentName;
	    }

	    public void setDocumentName(String documentName) {
	        this.documentName = documentName;
	    }

	    public String getCreatedOn() {
	        return createdOn;
	    }

	    public void setCreatedOn(String createdOn) {
	        this.createdOn = createdOn;
	    }

	    public String getSharedWith() {
	        return sharedWith;
	    }

	    public void setSharedWith(String sharedWith) {
	        this.sharedWith = sharedWith;
	    }

	    public String getDocumentID() {
	        return documentID;
	    }

	    public void setDocumentID(String documentID) {
	        this.documentID = documentID;
	    }

	    public String getSharedBy() {
	        return sharedBy;
	    }

	    public void setSharedBy(String sharedBy) {
	        this.sharedBy = sharedBy;
	    }

	    public String getOwner() {
	        return owner;
	    }

	    public void setOwner(String owner) {
	        this.owner = owner;
	    }

	    public ArrayList getActiveUsers() {
	        return activeUsers;
	    }

	    public void setActiveUsers(ArrayList<String> activeUsers) {
	        this.activeUsers = activeUsers;
	    }
	    
	  
}