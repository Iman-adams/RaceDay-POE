# RaceDay event and race management system
## YouTube video
https://youtu.be/QU9NrNSRq3k

## CI/CD
<img width="1491" height="543" alt="image" src="https://github.com/user-attachments/assets/f423ad2f-8241-4233-adde-9cb4dd781121" />

## Section A
### Overview
- Section A establishes the relational model for the entire RaceDay system. The database is structured to prevent redundant data storage while maintaining strict referential integrity across all entities.
<img width="1493" height="652" alt="RaceDay_ERD" src="https://github.com/user-attachments/assets/79fd527a-96e4-404b-b5c8-b8e727d22223" />

## Section B
### Overview
- Section B details the functional API architecture required for client-server interaction. The specification outlines HTTP verbs, endpoint routing rules, payload requirements, authentication constraints, and standardized HTTP response status codes.
<img width="1768" height="695" alt="Screenshot 2026-09-04 094535" src="https://github.com/user-attachments/assets/9844944b-e045-4ef1-acb8-e056cc06ce6b" />
<img width="1764" height="857" alt="Screenshot 2026-09-04 094604" src="https://github.com/user-attachments/assets/6e1aa2f8-44c4-4491-8810-88faa05ba00f" />
<img width="1792" height="592" alt="Screenshot 2026-09-04 094637" src="https://github.com/user-attachments/assets/061e7d0a-33ca-4c70-b033-842ffe30476a" />
<img width="1778" height="527" alt="Screenshot 2026-09-04 094655" src="https://github.com/user-attachments/assets/3b5c04a9-9a77-41bd-919d-9926d06a73db" />
<img width="1767" height="527" alt="Screenshot 2026-09-04 094709" src="https://github.com/user-attachments/assets/bd84090f-d91d-4f6e-95c2-61b6dfc82782" />


## Section C
### Overview
- Section C provides the complete T-SQL database script required to build, structure, and populate the physical RaceDay database in SQL Server.
- It defines all six tables using structured 3-digit primary key series (100s through 600s) while strictly enforcing foreign key relationships between users, venues, events, categories, enrollments, and results.
- It includes realistic sample seed data to test the system and ends with verification SELECT queries to instantly check that all tables were created and populated correctly.
How it the it looks when all the tables are executed:
<img width="1111" height="580" alt="image" src="https://github.com/user-attachments/assets/e8f7bb6e-1ea9-4f66-a7bf-c022ee20298e" />
