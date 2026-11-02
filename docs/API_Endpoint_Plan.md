# RaceDay - API Endpoint Plan

| Entity | HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Users** | **POST** | `/api/auth/register` | Registers a new user account | None (Public) | `{"FullName": "String", "Email": "String", "PasswordHash": "String", "Role": "String", "Gender": "String", "DateOfBirth": "Date"}` | `201 Created` - `{"User_ID": 101, "Email": "user@example.com"}`<br>`400 Bad Request`<br>`409 Conflict` |
| | **POST** | `/api/auth/login` | Authenticates a user and returns a session/token | None (Public) | `{"Email": "String", "PasswordHash": "String"}` | `200 OK` - `{"User_ID": 101, "Role": "Participant"}`<br>`401 Unauthorized` |
| | **GET** | `/api/users` | Retrieves all users | Admin | None | `200 OK` - Array of User objects |
| | **GET** | `/api/users/{id}` | Retrieves user profile details | Any (Logged in) | None | `200 OK` - User object<br>`404 Not Found` |
| | **PUT** | `/api/users/{id}` | Updates personal user profile information | Any (Logged in) | `{"FullName": "String", "Email": "String", "PasswordHash": "String", "Role": "String", "Gender": "String", "DateOfBirth": "Date"}` | `200 OK` - Updated User object<br>`400 Bad Request`<br>`404 Not Found` |
| | **DELETE** | `/api/users/{id}` | Deletes a user account | Admin | None | `204 No Content`<br>`404 Not Found` |
| **Venues** | **GET** | `/api/venues` | Retrieves all available event venues | None (Public) | None | `200 OK` - Array of Venue objects |
| | **GET** | `/api/venues/{id}` | Retrieves specific venue details | None (Public) | None | `200 OK` - Venue object<br>`404 Not Found` |
| | **POST** | `/api/venues` | Creates a new venue | Organiser / Admin | `{"Name": "String", "City": "String", "Province": "String", "Address": "String"}` | `201 Created` - Created Venue object<br>`400 Bad Request` |
| | **PUT** | `/api/venues/{id}` | Updates an existing venue | Organiser / Admin | `{"Name": "String", "City": "String", "Province": "String", "Address": "String"}` | `200 OK` - Updated Venue object<br>`404 Not Found` |
| | **DELETE** | `/api/venues/{id}` | Deletes a venue | Admin | None | `204 No Content`<br>`404 Not Found` |
| **Events** | **GET** | `/api/events` | Retrieves all upcoming events | None (Public) | None | `200 OK` - Array of Event objects |
| | **GET** | `/api/events/{id}` | Retrieves specific event details | None (Public) | None | `200 OK` - Event object including Organiser_ID and Venue_ID<br>`404 Not Found` |
| | **POST** | `/api/events` | Creates a new event | Organiser | `{"Organiser_ID": 101, "Venue_ID": 201, "Title": "String", "Description": "String", "EventDate": "DateTime", "Status": "String", "EventType": "String"}` | `201 Created` - Created Event object<br>`400 Bad Request` |
| | **PUT** | `/api/events/{id}` | Updates an existing event | Organiser | `{"Organiser_ID": 101, "Venue_ID": 201, "Title": "String", "Description": "String", "EventDate": "DateTime", "Status": "String", "EventType": "String"}` | `200 OK` - Updated Event object<br>`404 Not Found` |
| | **DELETE** | `/api/events/{id}` | Deletes an event | Organiser / Admin | None | `204 No Content`<br>`404 Not Found` |
| **EventCategories** | **GET** | `/api/categories` | Retrieves all event categories | None (Public) | None | `200 OK` - Array of Category objects |
| | **GET** | `/api/categories/{id}` | Retrieves a specific category | None (Public) | None | `200 OK` - Category object<br>`404 Not Found` |
| | **GET** | `/api/events/{id}/categories` | Lists all race categories for an event | None (Public) | None | `200 OK` - Array of Category objects<br>`404 Not Found` |
| | **POST** | `/api/events/{id}/categories` | Adds a category to an event | Organiser | `{"CategoryName": "String", "DistanceKm": 10.5, "EntryFee": 150.00, "MaxParticipants": 500, "StartDateTime": "DateTime", "MinAge": 16, "RouteDescription": "String"}` | `201 Created` - Created Category object<br>`400 Bad Request` |
| | **PUT** | `/api/categories/{id}` | Updates an existing category | Organiser | `{"Event_ID": 301, "CategoryName": "String", "DistanceKm": 10.5, "EntryFee": 150.00, "MaxParticipants": 500, "StartDateTime": "DateTime", "MinAge": 16, "RouteDescription": "String"}` | `200 OK` - Updated Category object<br>`404 Not Found` |
| | **DELETE** | `/api/categories/{id}` | Deletes a category | Organiser / Admin | None | `204 No Content`<br>`404 Not Found` |
| **Enrollments** | **GET** | `/api/enrollments` | Retrieves all enrollments | Admin | None | `200 OK` - Array of Enrollment objects |
| | **GET** | `/api/enrollments/{id}` | Retrieves specific enrollment details | Any (Logged in) | None | `200 OK` - Enrollment object<br>`404 Not Found` |
| | **GET** | `/api/enrollments/user/{userId}` | Gets all enrollments for a participant | Participant | None | `200 OK` - Array of Enrollment objects |
| | **POST** | `/api/enrollments` | Registers a participant for an event category | Participant | `{"User_ID": 103, "Category_ID": 401, "EnrollmentDate": "Date", "PaymentStatus": "Paid", "BibNumber": "1001", "EmergencyContact": "0821234567"}` | `201 Created` - Created Enrollment object<br>`400 Bad Request` |
| | **PUT** | `/api/enrollments/{id}` | Updates enrollment details | Organiser / Admin | `{"User_ID": 103, "Category_ID": 401, "EnrollmentDate": "Date", "PaymentStatus": "Paid", "BibNumber": "1001", "EmergencyContact": "0821234567"}` | `200 OK` - Updated Enrollment object<br>`404 Not Found` |
| | **DELETE** | `/api/enrollments/{id}` | Cancels/deletes an enrollment | Participant / Admin | None | `204 No Content`<br>`404 Not Found` |
| **Results** | **GET** | `/api/results` | Retrieves all race results | None (Public) | None | `200 OK` - Array of Result objects |
| | **GET** | `/api/results/{id}` | Retrieves a specific race result | None (Public) | None | `200 OK` - Result object<br>`404 Not Found` |
| | **GET** | `/api/results/category/{categoryId}` | Gets results leaderboard for a category | None (Public) | None | `200 OK` - Array of Result objects showing OverallRank and CategoryRank |
| | **POST** | `/api/results` | Records official race result for an enrollment | Organiser | `{"Enrollment_ID": 501, "FinishTimeMinutes": 45.5, "OverallRank": 1, "Completed": 1, "CategoryRank": 1}` | `201 Created` - Created Result object<br>`400 Bad Request` |
| | **PUT** | `/api/results/{id}` | Updates an existing race result | Organiser | `{"Enrollment_ID": 501, "FinishTimeMinutes": 45.5, "OverallRank": 1, "Completed": 1, "CategoryRank": 1}` | `200 OK` - Updated Result object<br>`404 Not Found` |
| | **DELETE** | `/api/results/{id}` | Deletes a race result | Organiser / Admin | None | `204 No Content`<br>`404 Not Found` |