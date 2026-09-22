<script lang="ts" src="./BasketBuilderView.ts"></script>

<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-header__title">{{ t('basket.title') }}</h1>
        <p class="page-header__subtitle">{{ t('basket.subtitle') }}</p>
      </div>

      <div class="inline-actions basket-header__actions">
        <el-button :loading="basketsStore.isLoading" @click="refresh">
          {{ t('basket.refresh') }}
        </el-button>
        <el-button @click="openDialog('create')">{{ t('basket.create') }}</el-button>
        <el-button :disabled="isReadOnly" @click="openDialog('rename')">
          {{ t('basket.rename') }}
        </el-button>
        <el-button :disabled="isReadOnly" @click="openDialog('clone')">
          {{ t('basket.clone') }}
        </el-button>
        <el-button
          type="primary"
          :disabled="isReadOnly || !weightsValid"
          @click="openDialog('publish')"
        >
          {{ t('basket.publish') }}
        </el-button>
        <el-button type="warning" plain :disabled="isReadOnly" @click="openDialog('archive')">
          {{ t('basket.archive') }}
        </el-button>
      </div>
    </div>

    <el-alert
      v-if="basketsStore.hasLoadFailure"
      class="basket-alert"
      type="error"
      :closable="false"
      show-icon
      :title="t('common.sessionExpired')"
    />

    <div class="panel basket-panel">
      <el-row :gutter="16" align="middle">
        <el-col :xs="24" :md="10">
          <span class="panel-title">{{ t('basket.registry') }}</span>
          <el-select
            class="basket-registry__select"
            :model-value="basketsStore.selectedBasketId"
            :placeholder="t('basket.noSelection')"
            :disabled="basketsStore.baskets.length === 0"
            @update:model-value="selectBasket(String($event))"
          >
            <el-option
              v-for="item in basketsStore.baskets"
              :key="item.basketId"
              :label="item.name"
              :value="item.basketId"
            >
              <span class="basket-registry__option">
                <span>{{ item.name }}</span>
                <el-tag :type="statusTagType(item.status)" size="small" effect="plain">
                  {{ t(`basketStatus.${item.status}`) }}
                </el-tag>
              </span>
            </el-option>
          </el-select>
        </el-col>

        <el-col :xs="24" :md="14">
          <span class="panel-title">{{ t('basket.activeBasket') }}</span>
          <el-alert
            v-if="basketsStore.activeBasket"
            type="success"
            :closable="false"
            show-icon
            :title="`${basketsStore.activeBasket.name} · v${basketsStore.activeBasket.activeVersionNumber}`"
          />
          <el-alert
            v-else
            type="info"
            :closable="false"
            show-icon
            :title="t('basket.noActiveBasket')"
          />
        </el-col>
      </el-row>

      <p v-if="basketsStore.baskets.length === 0" class="basket-empty">
        {{ t('basket.noBaskets') }}
      </p>

      <template v-if="detail">
        <el-alert
          v-if="isArchived"
          class="basket-alert"
          type="warning"
          :closable="false"
          show-icon
          :title="t('basket.readOnlyArchived')"
        />

        <span class="panel-title basket-versions__title">{{ t('basket.versions') }}</span>
        <el-table :data="versions" :empty-text="t('basket.noVersions')" size="small">
          <el-table-column :label="t('basket.version')" width="96">
            <template #default="scope">
              <strong>v{{ scope.row.number }}</strong>
            </template>
          </el-table-column>
          <el-table-column :label="t('basket.createdAt')" width="180">
            <template #default="scope">{{ formatTimestamp(scope.row.createdAtUtc) }}</template>
          </el-table-column>
          <el-table-column :label="t('basket.versionStatusColumn')" width="130">
            <template #default="scope">
              <el-tag :type="versionTagType(scope.row.status)" size="small" effect="plain">
                {{ t(`basketVersionStatus.${scope.row.status}`) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column :label="t('basket.versionNote')" min-width="200">
            <template #default="scope">{{ scope.row.note }}</template>
          </el-table-column>
          <el-table-column width="120" align="right">
            <template #default="scope">
              <el-button
                link
                type="primary"
                :disabled="isArchived || scope.row.status === 'Active'"
                @click="openDialog('activate', scope.row.versionId)"
              >
                {{ t('basket.activate') }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </template>
    </div>

    <div v-if="detail" class="panel basket-panel">
      <span class="panel-title">{{ t('basket.composition') }}</span>
      <p class="basket-hint">{{ t('basket.compositionHint') }}</p>

      <el-row :gutter="16" class="basket-strip">
        <el-col :xs="12" :sm="6">
          <span class="basket-strip__label">{{ t('basket.selectedLegs') }}</span>
          <strong class="basket-strip__value"
            >{{ selectedLegs.length }}/{{ draftLegs.length }}</strong
          >
        </el-col>
        <el-col :xs="12" :sm="6">
          <span class="basket-strip__label">{{ t('basket.selectedWeight') }}</span>
          <strong
            class="basket-strip__value"
            :class="{ 'basket-strip__value--invalid': !weightsValid }"
          >
            {{ selectedWeight }}%
          </strong>
        </el-col>
        <el-col :xs="24" :sm="12" class="basket-strip__actions">
          <el-button :disabled="isReadOnly" @click="normalizeWeights">
            {{ t('basket.normalize') }}
          </el-button>
          <el-button
            type="primary"
            :loading="basketsStore.isSaving"
            :disabled="!canSaveComposition"
            @click="saveComposition"
          >
            {{ t('basket.saveComposition') }}
          </el-button>
        </el-col>
      </el-row>

      <el-alert
        v-if="!weightsValid && selectedLegs.length > 0"
        class="basket-alert"
        type="warning"
        :closable="false"
        show-icon
        :title="t('basket.compositionInvalidWeights')"
      />

      <div class="inline-actions basket-add-leg">
        <el-input
          v-model="newLegSymbol"
          class="basket-add-leg__input"
          maxlength="32"
          :disabled="isReadOnly"
          :placeholder="t('basket.legSymbolPlaceholder')"
          @keyup.enter="addLeg"
        />
        <el-button :disabled="isReadOnly" @click="addLeg">{{ t('basket.addLeg') }}</el-button>
      </div>

      <BasketLegTable v-model:legs="draftLegs" :disabled="isReadOnly" />
    </div>

    <div v-if="detail" class="panel basket-panel">
      <div class="panel-header">
        <div>
          <span class="panel-title">{{ t('basket.strategy') }}</span>
          <p class="basket-hint">{{ t('basket.strategyHint') }}</p>
        </div>
        <el-tag v-if="detail.activePolicy" effect="plain" size="small">
          {{ t('basket.versionInForce') }} v{{ detail.activeVersionNumber }}
        </el-tag>
      </div>

      <el-form label-position="top">
        <el-row :gutter="16">
          <el-col :xs="24" :md="12">
            <el-form-item :label="t('basket.failurePolicy')">
              <el-select v-model="draftPolicy.failurePolicy" :disabled="isReadOnly">
                <el-option
                  v-for="policy in ['MinimumCoverage', 'AllOrNothing', 'RequireConfirmation']"
                  :key="policy"
                  :label="t(`failurePolicy.${policy}`)"
                  :value="policy"
                />
              </el-select>
            </el-form-item>
          </el-col>

          <el-col :xs="24" :md="12">
            <el-form-item :label="t('basket.minimumCoverage')">
              <el-slider
                v-model="draftPolicy.minimumCoverage"
                :min="50"
                :max="100"
                :disabled="isReadOnly"
                show-input
              />
            </el-form-item>
          </el-col>

          <el-col :xs="24" :md="12">
            <el-form-item :label="t('basket.riskPerBasket')">
              <el-input-number
                v-model="draftPolicy.riskPerBasket"
                :min="0.1"
                :max="10"
                :step="0.1"
                :precision="1"
                :disabled="isReadOnly"
              />
            </el-form-item>
          </el-col>

          <el-col :xs="24" :md="12">
            <el-form-item :label="t('basket.dailyLossLimit')">
              <el-input-number
                v-model="draftPolicy.dailyLossLimit"
                :min="0.1"
                :max="20"
                :step="0.1"
                :precision="1"
                :disabled="isReadOnly"
              />
            </el-form-item>
          </el-col>

          <el-col :xs="24" :md="12">
            <el-form-item :label="t('basket.combinationMode')">
              <el-select v-model="draftPolicy.combinationMode" :disabled="isReadOnly">
                <el-option
                  v-for="mode in ['WeightedEnsemble', 'Consensus', 'RegimeGated']"
                  :key="mode"
                  :label="t(`strategyCombination.${mode}`)"
                  :value="mode"
                />
              </el-select>
            </el-form-item>
          </el-col>

          <el-col :xs="24" :md="12">
            <el-form-item :label="t('basket.conflictPolicy')">
              <el-select v-model="draftPolicy.conflictPolicy" :disabled="isReadOnly">
                <el-option
                  v-for="policy in ['NoTrade', 'RequireReview', 'FollowHighestWeight']"
                  :key="policy"
                  :label="t(`strategyConflict.${policy}`)"
                  :value="policy"
                />
              </el-select>
            </el-form-item>
          </el-col>

          <el-col :xs="24" :sm="12">
            <el-form-item :label="t('basket.minimumAgreement')">
              <el-input-number
                v-model="draftPolicy.minimumAgreement"
                :min="50"
                :max="100"
                :step="5"
                :disabled="isReadOnly"
              />
            </el-form-item>
          </el-col>

          <el-col :xs="24" :sm="12">
            <el-form-item :label="t('basket.minimumStrategyConfidence')">
              <el-input-number
                v-model="draftPolicy.minimumStrategyConfidence"
                :min="1"
                :max="100"
                :step="5"
                :disabled="isReadOnly"
              />
            </el-form-item>
          </el-col>
        </el-row>

        <div class="basket-strategy-components">
          <div class="panel-header">
            <div>
              <span class="panel-title">{{ t('basket.strategyComponents') }}</span>
              <p class="basket-hint">{{ t('basket.strategyComponentsHint') }}</p>
            </div>
          </div>

          <el-table :data="draftPolicy.components" size="small">
            <el-table-column :label="t('basket.include')" width="88">
              <template #default="scope">
                <el-switch v-model="scope.row.enabled" :disabled="isReadOnly" />
              </template>
            </el-table-column>
            <el-table-column :label="t('basket.strategyComponent')" min-width="180">
              <template #default="scope">
                {{ t(`strategyComponent.${scope.row.type}`) }}
              </template>
            </el-table-column>
            <el-table-column :label="t('basket.timeFrame')" width="130">
              <template #default="scope">
                <el-select v-model="scope.row.timeFrame" :disabled="isReadOnly">
                  <el-option v-for="frame in ['M5', 'M15', 'M30', 'H1']" :key="frame" :label="frame" :value="frame" />
                </el-select>
              </template>
            </el-table-column>
            <el-table-column :label="t('basket.weight')" width="140">
              <template #default="scope">
                <el-input-number v-model="scope.row.weight" :min="1" :max="100" :disabled="isReadOnly" controls-position="right" />
              </template>
            </el-table-column>
          </el-table>
        </div>

        <el-button
          type="primary"
          :loading="basketsStore.isSaving"
          :disabled="!canSavePolicy"
          @click="savePolicy"
        >
          {{ t('basket.saveStrategy') }}
        </el-button>
      </el-form>
    </div>

    <RiskGatePanel
      v-if="detail"
      class="panel basket-panel"
      :decision="riskStore.decision"
      :is-loading="riskStore.isLoading"
      :has-load-failure="riskStore.hasLoadFailure"
      @refresh="loadRisk"
    />

    <RiskLimitsPanel
      v-if="detail"
      class="panel basket-panel"
      :limits="riskStore.limits"
      :is-loading="riskStore.isLoading"
    />

    <el-dialog
      :model-value="dialogMode !== null"
      :title="dialogTitle"
      width="min(32rem, 92vw)"
      @close="closeDialog"
    >
      <el-form v-if="isNameDialog" label-position="top">
        <el-form-item :label="t('basket.name')">
          <el-input
            v-model="nameInput"
            maxlength="128"
            show-word-limit
            :placeholder="t('basket.namePlaceholder')"
          />
        </el-form-item>
      </el-form>

      <template v-else-if="dialogMode === 'publish'">
        <el-alert
          class="basket-alert"
          type="info"
          :closable="false"
          show-icon
          :title="t('basket.publishBody')"
        />
        <el-alert
          v-if="compositionIsDirty || policyIsDirty"
          class="basket-alert"
          type="warning"
          :closable="false"
          show-icon
          :title="t('basket.publishPendingChanges')"
        />
        <el-form label-position="top">
          <el-form-item :label="t('basket.versionNote')">
            <el-input
              v-model="versionNote"
              maxlength="256"
              show-word-limit
              :placeholder="t('basket.versionNotePlaceholder')"
            />
          </el-form-item>
        </el-form>
      </template>

      <template v-else-if="dialogMode === 'activate'">
        <el-alert type="warning" :closable="false" show-icon :title="t('basket.activateBody')" />
      </template>

      <el-alert
        v-else-if="dialogMode === 'archive'"
        type="warning"
        :closable="false"
        show-icon
        :title="t('basket.archiveBody')"
      />

      <template #footer>
        <el-button @click="closeDialog">{{ t('common.cancel') }}</el-button>
        <el-button
          type="primary"
          :loading="basketsStore.isSaving"
          :disabled="confirmDisabled"
          @click="confirmDialog"
        >
          {{ t('common.confirm') }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped lang="less" src="./BasketBuilderView.less"></style>
