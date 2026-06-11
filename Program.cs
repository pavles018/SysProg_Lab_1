using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace TriviaServerVremenskoIsticanje
{
    public class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly Dictionary<string, CacheItem> cache = new Dictionary<string, CacheItem>();
        private static readonly HashSet<string> requestsInProgress = new HashSet<string>();
        private static readonly object cacheLock = new object();
        private static bool isActive = true;

        private const int CacheDurationSeconds = 120;

        static void Main(string[] args)
        {
            Console.WriteLine("Pokretanje Trivia web servera");
            Console.WriteLine("Server slusa na adresi: http://localhost:8080/");
            Console.WriteLine("Primer poziva:");
            Console.WriteLine("http://localhost:8080/api.php?amount=10&category=25&difficulty=medium\n");

            HttpListener listener = new HttpListener();

            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();

            Console.WriteLine("Server je pokrenut.\n");

            Thread ESCListenerThread = new Thread(() =>
            {
                while (isActive)
                {
                    ConsoleKeyInfo consoleKey = Console.ReadKey(true);
                    if (consoleKey.Key == ConsoleKey.Escape)
                    {
                        isActive = false;
                        listener.Stop();
                        Console.WriteLine("\nESC -> gasim server");
                        break;
                    }
                }
            });
            ESCListenerThread.Start();
             

            while (isActive)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();

                    Task.Run( () => ObradiZahtev(context));

                }
                catch
                {
                    if (!isActive)
                        break;
                }
            }
        }

        private static async Task ObradiZahtev(HttpListenerContext context)
        {
            try
            {
                string putanja = context.Request.Url!.AbsolutePath;

                if (putanja != "/" && putanja != "/api.php")
                {
                    PosaljiJson(context, "Nepostojeca ruta.", 404);
                    return;
                }

                string amount = context.Request.QueryString["amount"] ?? "";
                string category = context.Request.QueryString["category"] ?? "";
                string difficulty = context.Request.QueryString["difficulty"] ?? "";

                if (string.IsNullOrWhiteSpace(amount) ||
                    string.IsNullOrWhiteSpace(category) ||
                    string.IsNullOrWhiteSpace(difficulty))
                {
                    PosaljiJson(context,
                        "Greska: moras uneti amount, category i difficulty.",
                        400);

                    return;
                }

                string cacheKey =
                    $"amount={amount}&category={category}&difficulty={difficulty}";

                string query =
                    $"amount={amount}&category={category}&difficulty={difficulty}";

                Console.WriteLine($"[Nit {Thread.CurrentThread.ManagedThreadId}] Zahtev: {cacheKey}");

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                string json = await VratiPodatke(query, cacheKey);

                stopwatch.Stop();

                Console.WriteLine($"[Nit {Thread.CurrentThread.ManagedThreadId}] Zavrseno za: {stopwatch.ElapsedMilliseconds} ms\n");

                PosaljiJson(context, json, 200);
            }
            catch (Exception ex)
            {
                PosaljiJson(context, "Greska: " + ex.Message, 500);
            }
        }

        private static async Task<string> VratiPodatke(string query, string cacheKey)
        {
            lock (cacheLock)
            {
                if (cache.ContainsKey(cacheKey))
                {
                    CacheItem item = cache[cacheKey];

                    TimeSpan protekloVreme =
                        DateTime.Now - item.TimeCreated;

                    if (protekloVreme.TotalSeconds <= CacheDurationSeconds)
                    {
                        Console.WriteLine($"[Nit {Thread.CurrentThread.ManagedThreadId}] CACHE HIT");

                        return item.Json;
                    }
                    else
                    {
                        Console.WriteLine($"[Nit {Thread.CurrentThread.ManagedThreadId}] CACHE EXPIRED");

                        cache.Remove(cacheKey);
                    }
                }

                while (requestsInProgress.Contains(cacheKey))
                {
                    Console.WriteLine($"[Nit {Thread.CurrentThread.ManagedThreadId}] Ceka drugu nit");

                    Monitor.Wait(cacheLock);

                    if (cache.ContainsKey(cacheKey))
                    {
                        CacheItem item = cache[cacheKey];

                        TimeSpan protekloVreme =
                            DateTime.Now - item.TimeCreated;

                        if (protekloVreme.TotalSeconds <= CacheDurationSeconds)
                        {
                            Console.WriteLine($"[Nit {Thread.CurrentThread.ManagedThreadId}] CACHE HIT posle cekanja");

                            return item.Json;
                        }
                    }
                }

                Console.WriteLine($"[Nit {Thread.CurrentThread.ManagedThreadId}] CACHE MISS");

                requestsInProgress.Add(cacheKey);
            }

            try
            {
                string json = await PozoviOpenTrivia(query);

                lock (cacheLock)
                {
                    cache[cacheKey] = new CacheItem
                    {
                        Json = json,
                        TimeCreated = DateTime.Now
                    };

                    requestsInProgress.Remove(cacheKey);

                    Monitor.PulseAll(cacheLock);
                }

                Console.WriteLine($"[Nit {Thread.CurrentThread.ManagedThreadId}] Upisan u kes");

                return json;
            }
            catch
            {
                lock (cacheLock)
                {
                    requestsInProgress.Remove(cacheKey);

                    Monitor.PulseAll(cacheLock);
                }

                throw;
            }
        }

        private static async Task<string> PozoviOpenTrivia(string query)
        {
            string url =
                "https://opentdb.com/api.php?" + query;

            Task<HttpResponseMessage> responseTask = client.GetAsync(url);

            responseTask.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    Console.WriteLine($"[ContinueWith] API poziv zavrsen za: {url}");
                }
                else
                {
                    Console.WriteLine("[ContinueWith] API poziv nije uspeo.");
                }
            });

            HttpResponseMessage response =
                await responseTask;

            string json =
                await response.Content.ReadAsStringAsync();

            JObject parsed = JObject.Parse(json);

            int responseCode =
                parsed["response_code"]!.Value<int>();

            if (responseCode != 0)
            {
                throw new Exception(
                    "Navedena pitanja ne postoje.");
            }

            return json;
        }

        private static void PosaljiJson(
            HttpListenerContext context,
            string tekst,
            int statusCode)
        {
            byte[] buffer =
                Encoding.UTF8.GetBytes(tekst);

            context.Response.StatusCode = statusCode;

            context.Response.ContentType =
                "application/json; charset=utf-8";

            context.Response.ContentLength64 =
                buffer.Length;

            context.Response.OutputStream.Write(
                buffer,
                0,
                buffer.Length);

            context.Response.OutputStream.Close();
        }
    }

    public class CacheItem
    {
        public string Json { get; set; } = "";

        public DateTime TimeCreated { get; set; }
    }
}