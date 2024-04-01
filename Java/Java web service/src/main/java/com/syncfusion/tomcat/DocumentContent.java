package com.syncfusion.tomcat;
import java.util.List;

import com.syncfusion.ej2.wordprocessor.ActionInfo;
public class DocumentContent {
	 private int version;
	    private String sfdt;
	    private List<ActionInfo> actions;

	    // Default constructor
	    public DocumentContent() {
	    }

	    // Parameterized constructor
	
	  public DocumentContent(int version, String sfdt, List<ActionInfo> actions) {
	  this.version = version; this.sfdt = sfdt; this.actions = actions; }
	 

	    // Getter and setter methods
	    public int getVersion() {
	        return version;
	    }

	    public void setVersion(int version) {
	        this.version = version;
	    }

	    public String getSfdt() {
	        return sfdt;
	    }

	    public void setSfdt(String sfdt) {
	        this.sfdt = sfdt;
	    }

	
	  public List<ActionInfo> getActions() { return actions; }
	  
	  public void setActions(List<ActionInfo> actions) { this.actions = actions; }
	 
}
