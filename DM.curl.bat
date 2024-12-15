@echo off

REM --------------------------------------------------
REM Ensure the current directory is correct
echo Current Directory: %CD%
echo Checking if files exist...

if not exist "%CD%\document1.pdf" echo "Error: document1.pdf not found!" && exit /b
if not exist "%CD%\document2.pdf" echo "Error: document2.pdf not found!" && exit /b
if not exist "%CD%\document3.pdf" echo "Error: document3.pdf not found!" && exit /b

REM --------------------------------------------------
echo 1) Upload multiple documents
echo.

echo Uploading document1.pdf:
curl -L -X POST http://localhost/api/Document -H "Accept: application/json" -H "Content-Type: multipart/form-data" -F "name=Document One" -F "author=John Doe" -F "file=@%CD%\document1.pdf" -s -w "\n%{time_total}s"

echo Uploading document2.pdf:
curl -L -X POST http://localhost/api/Document -H "Accept: application/json" -H "Content-Type: multipart/form-data" -F "name=Document Two" -F "author=John Doe" -F "file=@%CD%\document2.pdf" -s -w "\n%{time_total}s"

echo Uploading document3.pdf:
curl -L -X POST http://localhost/api/Document -H "Accept: application/json" -H "Content-Type: multipart/form-data" -F "name=Document Three" -F "author=John Doe" -F "file=@%CD%\document3.pdf" -s -w "\n%{time_total}s"

echo.

pause


REM --------------------------------------------------
echo 2) Get all documents
echo.

curl -L -X GET http://localhost/api/Document | echo.

REM Continue with other commands...

REM --------------------------------------------------
echo 3) Search for documents
echo.

echo Searching for "document":
curl -L -X POST http://localhost/api/Document/search -H "Accept: application/json" -H "Content-Type: application/json" -d "{\"searchTerm\":\"document\"}"

echo Searching for empty term:
curl -L -X POST http://localhost/api/Document/search -H "Accept: application/json" -H "Content-Type: application/json" -d "{\"searchTerm\":\"\"}"

REM --------------------------------------------------
echo 4) Get a document by ID
echo.

echo Getting existing document:
curl -L -X GET http://localhost/api/Document/1 | echo.

echo Trying to get non-existent document:
curl -L -X GET http://localhost/api/Document/99 | echo.


REM --------------------------------------------------
echo 5) Delete a document
echo.

echo Deleting existing document:
curl -L -X DELETE http://localhost/api/Document/1 | echo.

echo Trying to delete non-existent document:
curl -L -X DELETE http://localhost/api/Document/99999 | echo.

REM --------------------------------------------------
echo 6) Test edge cases
echo.

echo Trying to upload document with invalid name:
curl -L -X POST http://localhost/api/Document -H "Accept: application/json" -H "Content-Type: multipart/form-data" -F "name=" -F "author=John Doe" -F "file=@%CD%\invalid_name.pdf"

echo Trying to search for deleted document:
curl -L -X POST http://localhost/api/Document/search -H "Accept: application/json" -H "Content-Type: application/json" -d "{\"searchTerm\":\"deleted_document\"}"

echo End of tests
pause
