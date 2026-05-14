package com.ssafy.server.domain.room.dto.request;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RoomCreateRequest {
    @NotBlank(message = "sessionName은 필수입니다.")
    private String sessionName;

    @NotBlank(message = "roomName은 필수입니다.")
    private String roomName;

    @NotBlank(message = "hostNickname은 필수입니다.")
    private String hostNickname;

    @NotBlank(message = "status는 필수입니다.")
    private String status;

    @NotNull(message = "playerCount는 필수입니다.")
    @Min(value = 0, message = "playerCount는 0 이상이어야 합니다.")
    private Integer playerCount;

    @NotNull(message = "maxPlayers는 필수입니다.")
    @Min(value = 1, message = "maxPlayers는 1 이상이어야 합니다.")
    private Integer maxPlayers;

    @NotBlank(message = "workerId는 필수입니다.")
    private String workerId;

    @NotBlank(message = "workerDisplayName은 필수입니다.")
    private String workerDisplayName;

    @NotBlank(message = "workerBaseUrl은 필수입니다.")
    private String workerBaseUrl;

    private Integer gameServerPid;
}
