#!/bin/bash

# Fix LoggerFactory ambiguity
sed -i '' 's/\([^.]\)LoggerFactory\./\1Xians.Lib.Common.Infrastructure.LoggerFactory./g' Common/Caching/CacheService.cs Common/Security/CertificateReader.cs

# Fix XiansContext reference
sed -i '' 's/Xians\.Lib\.Agents\.XiansContext/Xians.Lib.Agents.Core.XiansContext/g' Workflows/Scheduling/ScheduleActivities.cs

# Fix BuiltinWorkflow reference  
sed -i '' 's/Xians\.Lib\.Agents\.Workflows\.BuiltinWorkflow/Xians.Lib.Workflows.BuiltinWorkflow/g' Agents/A2A/A2AClient.cs

# Fix Knowledge namespace conflicts in KnowledgeService.cs
sed -i '' 's/GetKnowledge<Knowledge>/GetKnowledge<Models.Knowledge>/g' Agents/Knowledge/KnowledgeService.cs
sed -i '' 's/SetKnowledge(cacheKey, knowledge)/SetKnowledge(cacheKey, knowledge)/g' Agents/Knowledge/KnowledgeService.cs
sed -i '' 's/ReadFromJsonAsync<Knowledge>/ReadFromJsonAsync<Models.Knowledge>/g' Agents/Knowledge/KnowledgeService.cs
sed -i '' 's/\bnew Knowledge\b/new Models.Knowledge/g' Agents/Knowledge/KnowledgeService.cs
sed -i '' 's/ReadFromJsonAsync<List<Knowledge>>/ReadFromJsonAsync<List<Models.Knowledge>>/g' Agents/Knowledge/KnowledgeService.cs

# Fix weird Xians.Lib.Lib.Xians reference
sed -i '' 's/Xians\.Lib\.Lib\.Xians/Xians.Lib.Agents.Core.XiansContext/g' Agents/A2A/A2AClient.cs
