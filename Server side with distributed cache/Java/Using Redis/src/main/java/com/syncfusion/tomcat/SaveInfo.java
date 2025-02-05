package com.syncfusion.tomcat;

import com.syncfusion.ej2.wordprocessor.ActionInfo;
import java.util.List;

public class SaveInfo {
	private String roomName;
	private List<ActionInfo> actions;
	private String userId;
	private int version;
	private boolean partialSave;

	public void setVersion(int version) {
		this.version = version;
	}

	public void setActions(List<ActionInfo> clearedOperations) {
		this.actions = clearedOperations;
	}

	public void setPartialSave(boolean partialSave) {
		this.partialSave = partialSave;
	}

	public void setUserID(String userID) {
		this.userId = userID;
	}

	public void setRoomName(String roomName2) {
		this.roomName = roomName2;
	}

	public List<ActionInfo> getActions() {
		return actions;
	}

	public String getRoomName() {
		return roomName;
	}

	public Boolean getPartialSave() {
		return partialSave;
	}

}
