#!/bin/bash
sed -i 's/using CapitalUniversity.Core.Domain.Notifications.Enums;/using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.Enums;/g' src/2.Core/CapitalUniversity.Core.Abstractions/CrossCutting/Notifications/Dtos/NotificationDto.cs
sed -i 's/using CapitalUniversity.Core.Domain.Notifications.Enums;/using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.Enums;/g' src/2.Core/CapitalUniversity.Core.Abstractions/CrossCutting/Notifications/INotificationService.cs
sed -i 's/using CapitalUniversity.Core.Abstractions.CrossCutting.Logging.Enums;/using CapitalUniversity.Core.Abstractions.CrossCutting.Logging.Enums;/g' src/2.Core/CapitalUniversity.Core.Domain/Logging/LogEntry.cs
