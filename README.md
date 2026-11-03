# RaceDay event management system
## About RaceDay
RaceDay is a web-based event management system for the South African road running, walking and cycling community. Organisers create events, race categories and results. Participants browse events, enter a category, and track their own results.

## YouTube video
https://youtu.be/vbwbnePUmkU

## CI/CD workflow
The workflow in `.github/workflows/` runs on every push to `main`, `master` or the Part 2 branch, and on pull requests. It has two jobs:
- **validate-docs (Part 1):** fails the build if the `/docs` folder is missing, or if it does not contain the endpoint plan (`API_Endpoint_Plan.md` or `API_Plan.md`), `RaceDay.sql`, and the ERD image (`RaceDay_ERD.png`, `.jpg` or `.jpeg`).
- **build-and-test (Part 2):** installs .NET 8, restores the packages, builds `RaceDay.sln` in Release mode and runs every unit test. The tests use an in-memory database, so no SQL Server is needed on GitHub. The build fails if the code does not compile or any test fails.
<img width="1452" height="457" alt="image" src="https://github.com/user-attachments/assets/a5a97e69-1ff1-47a7-9f2e-379617f69fb6" />

# Part 1: System planning: ERD, API endpoint plan, SQL database script
## YouTube video
https://youtu.be/QU9NrNSRq3k
## CI/CD workflow
The workflow in .github/workflows/part1-validate.yml runs on every push. It checks that the /docs folder exists and contains the ERD, the endpoint plan and the SQL script, and that README.md is present.
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
<img width="1293" height="596" alt="Screenshot 2026-11-02 122919" src="https://github.com/user-attachments/assets/9a0d1153-783e-4c61-a9eb-c3dd96682673" />
