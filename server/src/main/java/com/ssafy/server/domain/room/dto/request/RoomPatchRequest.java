package com.ssafy.server.domain.room.dto.request;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RoomPatchRequest {
    @NotNull(message = "sessionName은 비어있으면 안됩니다")
    private String sessionName;

    private String status;

    @Min(value = 0, message = "playerCount는 0 이상이어야 합니다.")
    private Integer playerCount;
}
