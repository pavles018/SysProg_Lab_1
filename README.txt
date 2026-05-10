Zadatak 33 - Open Trivia Database server
Strategija kesa: vremensko isticanje

Tekst: 
Kreirati Web server koji klijentu omogućava pretragu pitanja za kvizove korišćenjem Open Trivia
Database API-a. Pretraga se može vršiti pomoću filtera koji se definišu u okviru query-a. Spisak
pitanja i odgovora koji zadovoljavaju uslov se vraćaju kao odgovor klijentu (pretragu vršiti po
kategoriji i težini). Svi zahtevi serveru se šalju preko browser-a korišćenjem GET metode. Ukoliko
navedena pitanja ne postoje, prikazati grešku klijentu.
Primer poziva serveru:
https://opentdb.com/api.php?amount=10&category=25&difficulty=medium
