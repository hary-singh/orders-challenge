# Orders Solution

## Overview
This project is a service for handling medical equipment orders. It includes functionalities for fetching, processing, and updating orders, as well as sending alerts for delivered items.

## Setup
1. Clone the repository:
    `git clone git@github.com:hary-singh/orders-challenge.git`
2. Navigate to the project directory:
    `cd Synapse.OrdersSolution`
3. Restore the dependencies:
    `dotnet restore`

## Running the Tests
To run the tests, use the following command:
`dotnet test`

## Configuration

Ensure that the `appsettings.json` file contains the correct API URLs:
```
{
  "ApiSettings": {
    "OrdersApiUrl": "https://api.example.com/orders",
    "AlertApiUrl": "https://api.example.com/alerts",
    "UpdateApiUrl": "https://api.example.com/update"
  }
}
```
