package com.syncfusion.tomcat;

import java.util.ArrayList;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.CommandLineRunner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.autoconfigure.security.servlet.SecurityAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.web.servlet.support.SpringBootServletInitializer;
import org.springframework.core.io.Resource;
import org.springframework.scheduling.annotation.EnableAsync;
import org.springframework.scheduling.annotation.EnableScheduling;
import org.springframework.web.bind.annotation.CrossOrigin;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.multipart.MultipartFile;
import com.syncfusion.ej2.wordprocessor.WordProcessorHelper;
import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.syncfusion.ej2.wordprocessor.FormatType;

import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.ProfileCredentialsProvider;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.model.ListObjectsV2Request;
import software.amazon.awssdk.services.s3.model.ListObjectsV2Response;
import software.amazon.awssdk.services.s3.model.S3Object;

@SpringBootApplication(exclude = { SecurityAutoConfiguration.class })
@RestController
@EnableAsync
@EnableScheduling
public class TomcatApplication extends SpringBootServletInitializer {
	@Value("${spring.datasource.accesskey}")
	private String datasourceAccessKey;
	@Value("${spring.datasource.secretkey}")
	private String datasourceSecretKey;
	@Value("${spring.datasource.bucketname}")
	private String datasourceBucketName;
	@Value("${spring.datasource.regionname}")
	private String datasourceRegionName;
	
	@Value("classpath:static/files/*")
	private Resource[] resources;
	
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

	
	@CrossOrigin(origins = "*", allowedHeaders = "*")
	@GetMapping("/api/wordeditor/GetDataSource")
	public String GetDataSource() throws Exception {
		ArrayList<FilesPathInfo> files = GetFilesInfo();
		ArrayList<FileNameInfo> dataSource = new ArrayList<FileNameInfo>();
		for (int i = 0; i < files.size(); i++) {
			dataSource.add(new FileNameInfo(i + 1, files.get(i).getFileName()));
		}
		GsonBuilder gsonBu = new GsonBuilder();
		Gson gson = gsonBu.disableHtmlEscaping().create();
		return gson.toJson(dataSource);
	}

	@CrossOrigin(origins = "*", allowedHeaders = "*")
	@GetMapping("/api/wordeditor/GetDataSourceS3")
	public String GetDataSourceS3() throws Exception {
		int i=0;
		ArrayList<FileNameInfo> dataSource = new ArrayList<FileNameInfo>();
		String bucketName = datasourceBucketName;

        // Create an S3 client
        S3Client s3Client = S3Client.builder()
                .region(Region.US_EAST_1) // Change to your bucket's region
                .credentialsProvider(StaticCredentialsProvider.create(
                                AwsBasicCredentials.create(datasourceAccessKey,datasourceSecretKey)
                        ))
                .build();
		ListObjectsV2Request listObjectsReq = ListObjectsV2Request.builder()
				.bucket(bucketName)
				.maxKeys(10) // Optional: limit the number of results
				.build();
		ListObjectsV2Response listObjectsRes;
		do {
			listObjectsRes = s3Client.listObjectsV2(listObjectsReq);

			for (S3Object s3Object : listObjectsRes.contents()) {
				System.out.println(" - " + s3Object.key() + " (size: " + s3Object.size() + " bytes)");
				dataSource.add(new FileNameInfo(i + 1, s3Object.key()));
			}

			// Set continuation token for pagination
			listObjectsReq = listObjectsReq.toBuilder()
					.continuationToken(listObjectsRes.nextContinuationToken())
					.build();

		} while (listObjectsRes.isTruncated());
		GsonBuilder gsonBu = new GsonBuilder();
		Gson gson = gsonBu.disableHtmlEscaping().create();
		return gson.toJson(dataSource);
	}

	private ArrayList<FilesPathInfo> GetFilesInfo() throws Exception {

		ArrayList<FilesPathInfo> filesInfo = new ArrayList<FilesPathInfo>();
		try {
			for (int i = 0; i < resources.length; i++) {
				FilesPathInfo path = new FilesPathInfo();
				path.setFileName(resources[i].getFilename());
				filesInfo.add(path);
			}
		} catch (Exception e) {
			throw new Exception("error", e);
		}
		return filesInfo;
	}
}
