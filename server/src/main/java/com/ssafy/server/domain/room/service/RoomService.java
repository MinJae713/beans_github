package com.ssafy.server.domain.room.service;

import com.ssafy.server.domain.room.dto.request.RoomCreateRequest;
import com.ssafy.server.domain.room.dto.response.RoomDeleteResponse;
import com.ssafy.server.domain.room.dto.request.RoomPatchRequest;
import com.ssafy.server.domain.room.dto.response.RoomResponse;

import java.util.List;

public interface RoomService {
    List<RoomResponse> getRooms();
    RoomResponse createRoom(RoomCreateRequest request);
    RoomResponse patchRoom(RoomPatchRequest request);
    RoomDeleteResponse deleteRoom(String sessionName);
}
