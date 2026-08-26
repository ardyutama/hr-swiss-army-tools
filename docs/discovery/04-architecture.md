# Architecture Design

## MVP V1 (focused)

A single ASP.NET Core SPA deployment backed by a database — everything containerized with
Docker.

```mermaid
flowchart LR
    subgraph spa["ASP.NET Core SPA (Docker)"]
        client["Client<br/>Vue"] --> backend[".NET Monolithic<br/>Backend Services"]
    end
    backend -.-> db["Database<br/>(Docker)"]
```

- **Client**: Vue SPA.
- **Backend**: .NET monolithic backend services.
- **Database**: runs in its own Docker container.

## Maybe final design

The monolith is split by responsibility into services, with queues and background workers
for the heavy parsing work.

```mermaid
flowchart LR
    subgraph spa["ASP.NET Core SPA (Docker)"]
        client["Client<br/>Vue"]
        client --> parserSvc["Parser Service"]
        client --> dataSvc["Data Service"]
        subgraph mono[".NET Monolithic"]
            parserSvc
            dataSvc
        end
    end
    subgraph queues["Docker"]
        parserSvc -.-> parserQueue["Parser Queue"]
        dataSvc -.-> dataQueue["Data Queue"]
    end
    subgraph workersBox["Docker"]
        parserQueue -.-> worker1(("Worker"))
        dataQueue -.-> worker1
        parserQueue -.-> worker2(("Worker"))
        dataQueue -.-> worker2
        worker1 -.-> db["Database"]
        worker2 -.-> db
    end
```

- **Parser Service** handles CV ingestion/parsing requests; **Data Service** handles data
  access requests.
- Both enqueue work into **Parser Queue** and **Data Queue**.
- **Workers** consume from the queues and persist results to the **Database**.
- Each deployable unit remains containerized with Docker.
