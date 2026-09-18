<script lang="ts" src="./LoginView.ts"></script>

<template>
  <div class="auth-view">
    <el-card class="auth-view__card">
      <template #header>
        <div>
          <strong>{{ t('auth.title') }}</strong>
        </div>
      </template>

      <p class="auth-view__subtitle">{{ t('auth.subtitle') }}</p>

      <el-form label-position="top" @submit.prevent="submit">
        <el-form-item :label="t('auth.userName')">
          <el-input v-model="credentials.userName" autocomplete="username" />
        </el-form-item>

        <el-form-item :label="t('auth.password')">
          <el-input
            v-model="credentials.password"
            type="password"
            show-password
            autocomplete="current-password"
          />
        </el-form-item>

        <el-button
          type="primary"
          native-type="submit"
          :loading="sessionStore.isBusy"
          class="auth-view__submit"
        >
          {{ t('auth.submit') }}
        </el-button>
      </el-form>
    </el-card>
  </div>
</template>

<style scoped lang="less" src="./LoginView.less"></style>
