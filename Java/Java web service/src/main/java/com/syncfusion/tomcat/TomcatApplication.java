package com.syncfusion.tomcat;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.web.servlet.support.SpringBootServletInitializer;
import org.springframework.web.bind.annotation.CrossOrigin;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.multipart.MultipartFile;
import com.syncfusion.ej2.wordprocessor.WordProcessorHelper;
import com.syncfusion.ej2.wordprocessor.FormatType;

@SpringBootApplication
@RestController
public class TomcatApplication extends SpringBootServletInitializer {
	@Override
	protected SpringApplicationBuilder configure(SpringApplicationBuilder application) {
		return application.sources(TomcatApplication.class);
	}

	public static void main(String[] args) {
		SpringApplication.run(TomcatApplication.class, args);
	}

	@CrossOrigin
	@RequestMapping(value = "/")
	public String hello() {
		return "Hello From Syncfusion Document Editor Java Service";
	}

	@CrossOrigin
	@RequestMapping(value = "/test")
	public String test() {
		System.out.println("==== in test ====");
		return "{\"sections\":[{\"blocks\":[{\"inlines\":[{\"texdocNamet\":\"Hello World\"}]}]}]}";
	}

	@CrossOrigin
	@RequestMapping(value = "/api/wordeditor/Import")
	public String uploadFile(@RequestParam("files") MultipartFile file) throws Exception {
		try {
			return WordProcessorHelper.load(file.getInputStream(), FormatType.Docx);
		} catch (Exception e) {
			e.printStackTrace();
			return "{\"sections\":[{\"blocks\":[{\"inlines\":[{\"text\":" + e.getMessage() + "}]}]}]}";
		}
	}

	@CrossOrigin
	@RequestMapping(value = "/api/wordeditor/RestrictEditing")
	public String[] restrictEditing(@RequestBody CustomRestrictParameter param) throws Exception {
		if (param.passwordBase64 == "" && param.passwordBase64 == null)
			return null;
		return WordProcessorHelper.computeHash(param.passwordBase64, param.saltBase64, param.spinCount);
	}

	@CrossOrigin
	@RequestMapping(value = "/api/wordeditor/SystemClipboard")
	public String systemClipboard(@RequestBody CustomParameter param) {
		if (param.content != null && param.content != "") {
			try {
				return WordProcessorHelper.loadString(param.content, GetFormatType(param.type.toLowerCase()));
			} catch (Exception e) {
				return "";
			}
		}
		return "";
	}

	static FormatType GetFormatType(String format) {
		switch (format) {
		case ".dotx":
		case ".docx":
		case ".docm":
		case ".dotm":
			return FormatType.Docx;
		case ".dot":
		case ".doc":
			return FormatType.Doc;
		case ".rtf":
			return FormatType.Rtf;
		case ".txt":
			return FormatType.Txt;
		case ".xml":
			return FormatType.WordML;
		case ".html":
			return FormatType.Html;
		default:
			return FormatType.Docx;
		}
	}

}
