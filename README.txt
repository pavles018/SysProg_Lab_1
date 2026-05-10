Zadatak 33 - Open Trivia Database server
Strategija kesa: vremensko isticanje

Pokretanje:
  dotnet run

Primeri poziva iz browsera:
  http://localhost:8080/api.php?amount=10&category=25&difficulty=medium
  http://localhost:8080/search?amount=5&category=18&difficulty=easy&type=multiple

Napomene:
- Server je konzolna C# aplikacija.
- Svi zahtevi idu GET metodom.
- Kes traje 120 sekundi.
- Ako stigne vise istih paralelnih zahteva, samo prvi ide ka Open Trivia API-ju, ostali cekaju rezultat.
- Korisceni su lock, Monitor.Wait i Monitor.PulseAll.
- Logovanje je samo u konzoli.
