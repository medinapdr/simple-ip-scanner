using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

class SimpleIPScanner
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Pressione Enter para pesquisar os IPs padrões ou informe a faixa (###.###.###) que deseja procurar:");
        string? input = Console.ReadLine();

        List<string> ipRanges;
        if (string.IsNullOrWhiteSpace(input))
        {
            // default IPs
            ipRanges = new List<string>() { "10.81.1", "10.14.104", "10.81.12", "10.81.13", " 10.81.14" };
        }
        else
        {
            ipRanges = new List<string>() { input };
        }

        bool foundResponsiveIPs = false;

        List<HostInfo> responsiveHosts = new List<HostInfo>();

        foreach (string ipRange in ipRanges)
        {
            Console.WriteLine();
            int totalIPs = 256;
            int completedIPs = 0;

            using (var countdownEvent = new CountdownEvent(totalIPs))
            {
                SemaphoreSlim semaphore = new SemaphoreSlim(32);

                for (int i = 0; i < totalIPs; i++)
                {
                    string ip = $"{ipRange}.{i}";

                    await semaphore.WaitAsync();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (await PingHost(ip))
                            {
                                string computerName = await GetComputerNameAsync(ip);
                                lock (responsiveHosts)
                                {
                                    responsiveHosts.Add(new HostInfo { IpAddress = ip, HostName = computerName });
                                }
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                            countdownEvent.Signal();
                            Interlocked.Increment(ref completedIPs);
                            double progress = (double)completedIPs / totalIPs * 100;
                            Console.Write($"\rVarrendo [{ipRange}.0/24] - {progress:F2}%");
                        }
                    });
                }

                while (!countdownEvent.IsSet)
                {
                    await Task.Delay(100);
                }

                if (countdownEvent.IsSet)
                {
                    Console.WriteLine($"\rVarredura [{ipRange}.0/24] - Concluída");
                }

                Console.WriteLine("========================================");
            }

            if (responsiveHosts.Any(h => h.IpAddress != null && h.IpAddress.StartsWith(ipRange)))
            {
                foundResponsiveIPs = true;
                Console.WriteLine("IPs responsivos:");
                foreach (var hostInfo in responsiveHosts.Where(h => h.IpAddress != null && h.IpAddress.StartsWith(ipRange)).OrderBy(h => GetLastPartOfIP(h.IpAddress)))
                {
                    Console.WriteLine($"{hostInfo.IpAddress} {(string.IsNullOrEmpty(hostInfo.HostName) ? "" : "-")} {hostInfo.HostName}");
                }

                double occupationPercentage = (double)responsiveHosts.Count(h => h.IpAddress != null && h.IpAddress.StartsWith(ipRange)) / totalIPs * 100;
                Console.WriteLine("========================================");
                Console.WriteLine($"Ocupação da faixa: {occupationPercentage:F2}%");

                if (responsiveHosts.Any(h => h.IpAddress != null && h.IpAddress.StartsWith(ipRange)))
                {
                    var responsiveIPsInRange = responsiveHosts.Where(h => h.IpAddress != null && h.IpAddress.StartsWith(ipRange)).Select(h => h.IpAddress).ToList();
                    var allIPsInRange = Enumerable.Range(0, totalIPs).Select(i => $"{ipRange}.{i}").ToList();

                    var nonResponsiveIPs = allIPsInRange.Except(responsiveIPsInRange).ToList();
                    if (nonResponsiveIPs.Any())
                    {
                        var random = new Random();
                        var randomIndex = random.Next(0, nonResponsiveIPs.Count);
                        var randomNonResponsiveIP = nonResponsiveIPs[randomIndex];
                        Console.WriteLine($"IP aleatório disponível: {randomNonResponsiveIP}");
                    }
                    else
                    {
                        Console.WriteLine($"Nenhum IP aleatório não responsivo encontrado na faixa {ipRange}.");
                    }
                }
                else
                {
                    Console.WriteLine("Nenhum IP responsivo encontrado.");
                }
            }
            else
            {
                Console.WriteLine("Nenhum IP responsivo encontrado.");
            }

            Console.WriteLine();
        }

        if (!foundResponsiveIPs)
        {
            Console.WriteLine("Nenhum IP responsivo encontrado em nenhuma faixa.");
        }

        Console.WriteLine("\nVarredura concluída. Pressione qualquer tecla para sair.");
        Console.ReadKey();
    }

    static async Task<bool> PingHost(string ip)
    {
        try
        {
            Ping ping = new Ping();
            PingReply reply = await ping.SendPingAsync(ip, 1000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    static int GetLastPartOfIP(string? ip)
    {
        if (ip != null)
        {
            string[] parts = ip.Split('.');
            if (parts.Length == 4 && int.TryParse(parts[3], out int lastPart))
            {
                return lastPart;
            }
        }
        return 0;
    }

    static async Task<string> GetComputerNameAsync(string ipAddress)
    {
        try
        {
            var hostEntry = await System.Net.Dns.GetHostEntryAsync(ipAddress);
            return hostEntry.HostName;
        }
        catch
        {
            return string.Empty;
        }
    }
}

class HostInfo
{
    public string? IpAddress { get; set; }
    public string? HostName { get; set; }
}
