# Case Workflow - AI-Driven Debt Collection

## Overview

This workflow demonstrates an AI-driven debt collection process with human-in-the-loop (HITL) controls at every customer communication point. The workflow ensures compliance, maintains human oversight, and provides a full audit trail of all actions.

## Workflow Activity Diagram

```mermaid
flowchart TD
    Start([Start: CaseWorkflow]) --> FetchData[Fetch Case Data]
    FetchData --> InitResult[Initialize Case Result]
    InitResult --> Assessment[AI Case Assessment]
    
    Assessment --> CheckRisk{Risk Level?}
    CheckRisk -->|High| EscalateHigh[Escalate to Human Handler]
    EscalateHigh --> EndHigh([End: Escalated])
    
    CheckRisk -->|Low/Medium| InitialEmail[Draft Initial Contact Email]
    
    InitialEmail --> HITL1{HITL: Review<br/>Initial Email}
    HITL1 -->|Reject/Escalate| Escalate1[Escalate to Human]
    Escalate1 --> End1([End: Escalated])
    
    HITL1 -->|Approve/Edit| SendEmail1[Send Initial Email]
    SendEmail1 --> Log1[Log: Email Sent]
    Log1 --> WaitCustomer[Wait for Customer Response]
    
    WaitCustomer --> ReceiveReply[Receive Customer Response]
    ReceiveReply --> DraftReply[AI Drafts Reply to Customer]
    
    DraftReply --> HITL2{HITL: Review<br/>Customer Reply}
    HITL2 -->|Reject/Escalate| Escalate2[Escalate to Human]
    Escalate2 --> End2([End: Escalated])
    
    HITL2 -->|Approve/Edit| SendReply[Send Reply to Customer]
    SendReply --> Log2[Log: Reply Sent]
    Log2 --> ProposePayment[AI Proposes Payment Plan]
    
    ProposePayment --> HITL3{HITL: Review<br/>Payment Plan}
    HITL3 -->|Reject/Escalate| Escalate3[Escalate to Human]
    Escalate3 --> End3([End: Escalated])
    
    HITL3 -->|Approve/Modify| SendPlan[Send Payment Plan to Customer]
    SendPlan --> Log3[Log: Payment Plan Sent]
    Log3 --> Monitor[Monitor Payment Schedule]
    
    Monitor --> MissedPayment[Detect Missed Payment]
    MissedPayment --> HITL4{HITL: Handle<br/>Missed Payment}
    
    HITL4 -->|Waive| LogWaive[Log: Payment Waived]
    LogWaive --> Resolved1([End: Resolved])
    
    HITL4 -->|Adjust Plan| LogAdjust[Log: Plan Adjusted]
    LogAdjust --> Resolved2([End: Resolved])
    
    HITL4 -->|Escalate| DirectEscalate[Escalate to Human]
    DirectEscalate --> End4([End: Escalated])
    
    HITL4 -->|Send Reminder| CheckEscalation[AI Evaluates Case Status]
    CheckEscalation --> HITL5{HITL: Escalation<br/>Decision}
    
    HITL5 -->|Escalate| FinalEscalate[Escalate to Human Handler]
    FinalEscalate --> End5([End: Escalated])
    
    HITL5 -->|Continue AI| ContinueAI[Continue AI Management]
    ContinueAI --> End6([End: Active - AI Managed])
    
    HITL5 -->|Close Case| CloseCase[Close Case]
    CloseCase --> End7([End: Resolved])
    
    HITL5 -->|Legal Action| LegalAction[Initiate Legal Process]
    LegalAction --> End8([End: Legal Action])

    style Start fill:#e1f5e1
    style HITL1 fill:#fff3cd
    style HITL2 fill:#fff3cd
    style HITL3 fill:#fff3cd
    style HITL4 fill:#fff3cd
    style HITL5 fill:#fff3cd
    style EndHigh fill:#f8d7da
    style End1 fill:#f8d7da
    style End2 fill:#f8d7da
    style End3 fill:#f8d7da
    style End4 fill:#f8d7da
    style End5 fill:#f8d7da
    style End6 fill:#d1ecf1
    style End7 fill:#d4edda
    style End8 fill:#d4edda
    style Resolved1 fill:#d4edda
    style Resolved2 fill:#d4edda
```

## Key Components

### Input Parameters
- **caseId**: Unique identifier for the debt collection case

### Human-in-the-Loop (HITL) Decision Points

1. **Initial Contact Email Review**
   - Actions: `approve`, `edit-and-approve`, `reject`, `escalate`
   - Purpose: Review AI-generated first contact email

2. **Customer Reply Response Review**
   - Actions: `approve`, `edit-and-approve`, `reject`, `escalate`
   - Context: Includes customer's original message
   - Purpose: Review AI response to customer inquiry

3. **Payment Plan Proposal Review**
   - Actions: `approve`, `modify`, `reject`, `escalate`
   - Purpose: Review AI-proposed payment terms (6-month plan)

4. **Missed Payment Action**
   - Actions: `send-reminder`, `adjust-plan`, `escalate`, `waive`
   - Purpose: Decide how to handle missed payment

5. **Escalation Decision**
   - Actions: `escalate`, `continue-ai`, `close-case`, `legal-action`
   - Purpose: Final decision on case handling strategy

### Escalation Triggers

The workflow automatically escalates to a human case handler when:
- Initial risk assessment is **High**
- Human rejects or requests escalation at any HITL point
- Multiple intervention attempts fail

### Case Result

The workflow returns a `CaseResult` object containing:
- Case and customer identifiers
- Outstanding amount
- Start and completion timestamps
- Final status
- Payment plan acceptance flag
- Complete activity log (audit trail)

## Possible Final States

| Status | Description |
|--------|-------------|
| **Escalated to Human** | Case requires human case handler intervention |
| **Active - AI Managed** | Case continues under AI management |
| **Resolved** | Payment plan executed successfully or case closed |

## Workflow Characteristics

- **AI-First**: AI orchestrates the entire process
- **Human-Controlled**: Humans approve all customer communications
- **Auditable**: Full activity log maintained
- **Compliant**: Multiple approval gates ensure compliance
- **Flexible**: Multiple exit paths based on case complexity

## Example Usage

```csharp
// Start workflow with just a case ID
var result = await workflow.RunAsync("CASE-2024-001");

// Result contains full history
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Payment Plan Accepted: {result.PaymentPlanAccepted}");
foreach (var log in result.ActivityLog)
{
    Console.WriteLine(log);
}
```

## Integration Points

- **Case Data Fetch**: Simulates retrieval from external database/CRM
- **Email Sending**: Would integrate with email service
- **Payment Processing**: Would integrate with Settle/Zeta or similar platforms
- **Task Management**: Uses Xians agent task system for HITL approvals

---

**Related Documentation**: See `ai_debt_collection_human_in_loop.md` for domain context and business requirements.
