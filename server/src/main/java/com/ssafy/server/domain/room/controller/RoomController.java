package com.ssafy.server.domain.room.controller;

import com.ssafy.server.domain.room.dto.request.RoomCreateRequest;
import com.ssafy.server.domain.room.dto.response.RoomDeleteResponse;
import com.ssafy.server.domain.room.dto.request.RoomPatchRequest;
import com.ssafy.server.domain.room.dto.response.RoomResponse;
import com.ssafy.server.domain.room.service.RoomService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/rooms")
@RequiredArgsConstructor
public class RoomController {
    private final RoomService roomService;

    @GetMapping
    public ResponseEntity<List<RoomResponse>> getRooms() {
        return ResponseEntity.ok(roomService.getRooms());
    }

    @PostMapping
    public ResponseEntity<RoomResponse> createRoom(@Valid @RequestBody RoomCreateRequest request) {
        return ResponseEntity.ok(roomService.createRoom(request));
    }

    @PatchMapping
    public ResponseEntity<RoomResponse> patchRoom(@Valid @RequestBody RoomPatchRequest request) {
        return ResponseEntity.ok(roomService.patchRoom(request));
    }

    @DeleteMapping("/{sessionName}")
    public ResponseEntity<RoomDeleteResponse> deleteRoom(@PathVariable String sessionName) {
        return ResponseEntity.ok(roomService.deleteRoom(sessionName));
    }
}
