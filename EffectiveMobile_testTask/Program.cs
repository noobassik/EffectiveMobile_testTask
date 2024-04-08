using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public class Program
{
	public static void Main(string[] args)
	{
		// Считать параметры из командной строки и/или файла конфигурации
		var arguments = ParseCommandLineArguments(args);
		var configParameters = LoadConfigParameters(arguments);

		// Объединить параметры командной строки и параметры конфигурации
		var parameters = MergeParameters(arguments, configParameters);

		// Указаны ли необходимые параметры
		if (!parameters.ContainsKey("file-log") || !parameters.ContainsKey("file-output") ||
			!parameters.ContainsKey("time-start") || !parameters.ContainsKey("time-end"))
		{
			Console.WriteLine("Required parameters are missing.");
			return;
		}

		string logFilePath = parameters["file-log"];
		string outputFile = parameters["file-output"];
		DateTime timeStart, timeEnd;

		// Попытка спарсить параметры нижней и верхней границы временного интервала
		if (!DateTime.TryParseExact(parameters["time-start"], "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out timeStart))
		{
			Console.WriteLine("Invalid time-start format.");
			return;
		}

		if (!DateTime.TryParseExact(parameters["time-end"], "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out timeEnd))
		{
			Console.WriteLine("Invalid time-end format.");
			return;
		}

		// Существует ли файл 
		if (!File.Exists(logFilePath))
		{
			Console.WriteLine("Log file does not exist or cannot be accessed.");
			return;
		}

		// Считывание файла и проверка каждой строки
		List<string> lines;
		try
		{
			lines = File.ReadAllLines(logFilePath).ToList();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error reading log file: {ex.Message}");
			return;
		}

		foreach (string line in lines)
		{
			// Валидация входного файла
			if (!IsValidLogLine(line))
			{
				Console.WriteLine($"Invalid log line format: {line}");
				return;
			}
		}

		// Фильтр по временному интервалу
		lines = lines.Where(line =>
		{
			string[] parts = line.Split(' ');
			DateTime timestamp;
			if (!DateTime.TryParseExact(parts[1] + " " + parts[2], "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out timestamp))
			{
				Console.WriteLine($"Invalid timestamp format in line: {line}");
				return false;
			}
			return timestamp >= timeStart && timestamp <= timeEnd;
		}).ToList();

		// Если указана нижняя граница диапазона адресов, то отфильтровать по их диапазону 
		if (parameters.ContainsKey("address-start"))
		{
			string addressStart = parameters["address-start"];
			string addressMask = parameters.ContainsKey("address-mask") ? parameters["address-mask"] : "255.255.255.255";
			lines = lines.Where(line =>
			{
				string[] parts = line.Split(' ');
				string ipAddress = parts[0];
				return IsInAddressRange(ipAddress, addressStart, addressMask);
			}).ToList();
		}

		// Спарсить каждую строку, извлекая IP-адреса.
		Dictionary<string, int> ipAddressCounts = new Dictionary<string, int>();
		foreach (string line in lines)
		{
			// Разделить каждую строку по пробелу, чтобы извлечь IP-адрес
			string ipAddress = line.Split(' ')[0];

			// Увеличение счетчика адресов
			if (ipAddressCounts.ContainsKey(ipAddress))
			{
				ipAddressCounts[ipAddress]++;
			}
			else
			{
				ipAddressCounts[ipAddress] = 1;
			}
		}

		// Запись результата в выходной файл
		using (StreamWriter writer = new StreamWriter(outputFile))
		{
			foreach (var kvp in ipAddressCounts)
			{
				writer.WriteLine($"{kvp.Key}: {kvp.Value}");
			}
		}
		Console.WriteLine("Analysis completed. Results written to the output file.");
	}

	static Dictionary<string, string> ParseCommandLineArguments(string[] args)
	{
		var arguments = new Dictionary<string, string>();
		for (int i = 0; i < args.Length; i += 2)
		{
			if (args.Length > i + 1 && args[i].StartsWith("--"))
			{
				arguments[args[i].Substring(2)] = args[i + 1];
			}
			else
			{
				Console.WriteLine("Invalid argument format.");
				Environment.Exit(1);
			}
		}
		return arguments;
	}

	public static Dictionary<string, string> LoadConfigParameters(Dictionary<string, string> arguments)
	{
		var configParameters = new Dictionary<string, string>();

		// Указан ли параметр файла конфигурации
		if (arguments.ContainsKey("config-file"))
		{
			string configFile = arguments["config-file"];
			try
			{
				string configJson = File.ReadAllText(configFile);
				configParameters = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading config file: {ex.Message}");
				Environment.Exit(1);
			}
		}

		return configParameters;
	}

	public static Dictionary<string, string> MergeParameters(Dictionary<string, string> arguments, Dictionary<string, string> configParameters)
	{
		var mergedParameters = new Dictionary<string, string>(configParameters);

		// Override config parameters with command line arguments
		foreach (var arg in arguments)
		{
			mergedParameters[arg.Key] = arg.Value;
		}

		return mergedParameters;
	}

	public static bool IsValidLogLine(string line)
	{
		// Соответствует ли строка ожидаемому формату
		string[] parts = line.Split(' ');
		if (parts.Length != 3)
		{
			return false;
		}

		DateTime timestamp;
		if (!DateTime.TryParseExact(parts[1] + " " + parts[2], "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out timestamp))
		{
			return false;
		}

		return true;
	}

	public static bool IsInAddressRange(string ipAddress, string addressStart, string addressMask)
	{
		byte[] ipBytes = ipAddress.Split('.').Select(byte.Parse).ToArray();
		byte[] startBytes = addressStart.Split('.').Select(byte.Parse).ToArray();
		byte[] maskBytes = addressMask.Split('.').Select(byte.Parse).ToArray();

		for (int i = 0; i < 4; i++)
		{
			if ((ipBytes[i] & maskBytes[i]) != (startBytes[i] & maskBytes[i]))
			{
				return false;
			}
		}
		return true;
	}
}
